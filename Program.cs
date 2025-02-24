using Pulumi;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Linq;
using Pulumi.Aws;
using Pulumi.Aws.Ec2;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi.Aws.AutoScaling;
using Pulumi.Aws.AutoScaling.Inputs;
using Pulumi.Aws.Route53;
using Pulumi.Aws.Route53.Inputs;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Alb;
using Pulumi.Aws.Alb.Inputs;
using Pulumi.Aws.Acm;

return await Deployment.RunAsync(() =>
{

    var awsProvider = new Provider("aws", new ProviderArgs
    {
        Region = Variables.aws_region
    });


    // Get VPC ID from environment variable
    var vpcId = System.Environment.GetEnvironmentVariable("VPC_ID") ??
        throw new Exception("VPC_ID environment variable is required");

    // Retrieve subnet IDs
    var getSubnets = GetSubnets.Invoke(new GetSubnetsInvokeArgs
    {
        Filters = new InputList<GetSubnetsFilterInputArgs>
        {
            new GetSubnetsFilterInputArgs
            {
                Name = "vpc-id",
                Values = new List<string> { vpcId }
            }
        }
    }, new InvokeOptions { Provider = awsProvider });

    // Retrieve the latest Amazon Linux 2023 AMI
    var getAmi = GetAmi.Invoke(new GetAmiInvokeArgs
    {
        MostRecent = true,
        Owners = new List<string> { "amazon" },
        Filters = new InputList<GetAmiFilterInputArgs>
        {
            new GetAmiFilterInputArgs
            {
                Name = "name",
                Values = new List<string> { "al2023-ami-*-kernel*x86_64*" }
            }
        }
    }, new InvokeOptions { Provider = awsProvider });

    // Create a security group for Load Balancer
    var securityGroup = new SecurityGroup("lb-security-group", new SecurityGroupArgs
    {
        VpcId = vpcId,
        Description = "Security group for Load Balancer",
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // Create a security group for the EC2 instances
    var instanceSecurityGroup = new SecurityGroup("instance-security-group", new SecurityGroupArgs
    {
        VpcId = vpcId,
        Description = "Security group for EC2 instances",
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // Add HTTPS ingress rule to the security group
    var httpsIngressRule = new SecurityGroupRule("https-inbound", new SecurityGroupRuleArgs
    {
        Type = "ingress",
        SecurityGroupId = securityGroup.Id,
        Protocol = "tcp",
        FromPort = 443,
        ToPort = 443,
        CidrBlocks = new[] { "0.0.0.0/0" },
        Description = "HTTPS inbound"
    }, new CustomResourceOptions { Provider = awsProvider });

    // Add HTTP ingress rule to the security group
    var httpIngressRule = new SecurityGroupRule("http-inbound", new SecurityGroupRuleArgs
    {
        Type = "ingress",
        SecurityGroupId = securityGroup.Id,
        Protocol = "tcp",
        FromPort = 80,
        ToPort = 80,
        CidrBlocks = new[] { "0.0.0.0/0" },
        Description = "HTTP inbound"
    }, new CustomResourceOptions { Provider = awsProvider });

    // Add egress rule to the security group
    var egressRule = new SecurityGroupRule("egress", new SecurityGroupRuleArgs
    {
        Type = "egress",
        SecurityGroupId = securityGroup.Id,
        Protocol = "-1",
        FromPort = 0,
        ToPort = 0,
        CidrBlocks = new[] { "0.0.0.0/0" },
        Description = "Egress"
    }, new CustomResourceOptions { Provider = awsProvider });

    // Add HTTP ingress rule to the instance security group
    var instanceIngressRule = new SecurityGroupRule("instance-http-inbound", new SecurityGroupRuleArgs
    {
        Type = "ingress",
        SecurityGroupId = instanceSecurityGroup.Id,
        Protocol = "tcp",
        FromPort = 80,
        ToPort = 80,
        SourceSecurityGroupId = securityGroup.Id
    }, new CustomResourceOptions { Provider = awsProvider });

    // Add egress rule to the instance security group
    var instanceEgressRule = new SecurityGroupRule("instance-egress", new SecurityGroupRuleArgs
    {
        Type = "egress",
        SecurityGroupId = instanceSecurityGroup.Id,
        Protocol = "-1",
        FromPort = 0,
        ToPort = 0,
        CidrBlocks = new[] { "0.0.0.0/0" },
        Description = "Egress"
    }, new CustomResourceOptions { Provider = awsProvider });

    // VPC Endpoints for SSM
    var ssmEndpoint = new VpcEndpoint("ssm-endpoint", new VpcEndpointArgs
    {
        VpcId = vpcId,
        ServiceName = $"com.amazonaws.{Variables.aws_region}.ssm",
        // ServiceName = "com.amazonaws.eu-west-1.ssm",  // hardcoded temporarily
        VpcEndpointType = "Interface",
        SubnetIds = getSubnets.Apply(s => s.Ids),
        SecurityGroupIds = new[] { instanceSecurityGroup.Id },
        PrivateDnsEnabled = true,
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // VPC Endpoint for EC2 messages
    var ec2MessagesEndpoint = new VpcEndpoint("ec2messages-endpoint", new VpcEndpointArgs
    {
        VpcId = vpcId,
        ServiceName = $"com.amazonaws.{Variables.aws_region}.ec2messages",
        // ServiceName = "com.amazonaws.eu-west-1.ec2messages",  // hardcoded temporarily
        VpcEndpointType = "Interface",
        SubnetIds = getSubnets.Apply(s => s.Ids),
        SecurityGroupIds = new[] { instanceSecurityGroup.Id },
        PrivateDnsEnabled = true,
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    var ssmmessagesEndpoint = new VpcEndpoint("ssmmessages-endpoint", new VpcEndpointArgs
    {
        VpcId = vpcId,
        ServiceName = $"com.amazonaws.{Variables.aws_region}.ssmmessages",
        // ServiceName = "com.amazonaws.eu-west-1.ssmmessages",  // hardcoded temporarily
        VpcEndpointType = "Interface",
        SubnetIds = getSubnets.Apply(s => s.Ids),
        SecurityGroupIds = new[] { instanceSecurityGroup.Id },
        PrivateDnsEnabled = true,
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // IAM role for the EC2 instances
    var role = new Role("ec2-role", new RoleArgs
    {
        AssumeRolePolicy = JsonConvert.SerializeObject(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Effect = "Allow",
                    Principal = new
                    {
                        Service = "ec2.amazonaws.com"
                    },
                    Action = "sts:AssumeRole"
                }
            }
        }),
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // Attach the AmazonSSMManagedInstanceCore managed policy to the role
    var policy = new RolePolicyAttachment("ssm-core-policy-attachment", new RolePolicyAttachmentArgs
    {
        PolicyArn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore",
        Role = role.Name
    }, new CustomResourceOptions { Provider = awsProvider });

    // Instance Profile for EC2 Instance
    var instanceProfile = new InstanceProfile("ec2-instance-profile", new InstanceProfileArgs
    {
        Role = role.Name,
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // User data for the EC2 instances
    var userData = @"#!/bin/bash
        dnf update -y
        dnf install -y https://s3.amazonaws.com/ec2-downloads-windows/SSMAgent/latest/linux_amd64/amazon-ssm-agent.rpm
        systemctl enable amazon-ssm-agent
        systemctl start amazon-ssm-agent
        dnf install -y httpd
        systemctl start httpd
        systemctl enable httpd
        echo ""<h1>Hello World from $(hostname -f)</h1>"" > /var/www/html/index.html";

    var launchTemplate = new LaunchTemplate("ec2-launch-template", new LaunchTemplateArgs
    {
        InstanceType = Variables.instance_type,
        ImageId = getAmi.Apply(ami => ami.Id),
        UserData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userData)),
        IamInstanceProfile = new LaunchTemplateIamInstanceProfileArgs
        {
            Name = instanceProfile.Name
        },
        NetworkInterfaces = new[]
        {
            new LaunchTemplateNetworkInterfaceArgs
            {
                DeviceIndex = 0,
                AssociatePublicIpAddress = "true",
                SubnetId = getSubnets.Apply(s => s.Ids.First()),
                SecurityGroups = new[] { instanceSecurityGroup.Id }
            }
        },
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });


    // Convert standard_tags dictionary to a list of dictionaries with propagate_at_launch key
    var asgTags = Variables.DefaultTags.Select(tag => new Pulumi.Aws.AutoScaling.Inputs.GroupTagArgs
    {
        Key = tag.Key,
        Value = tag.Value,
        PropagateAtLaunch = true
    }).ToList();

    // Auto Scaling Group - using explicit namespace
    var autoScalingGroup = new Pulumi.Aws.AutoScaling.Group("webserver-asg", new Pulumi.Aws.AutoScaling.GroupArgs
    {
        VpcZoneIdentifiers = getSubnets.Apply(s => s.Ids),
        LaunchTemplate = new GroupLaunchTemplateArgs
        {
            Id = launchTemplate.Id,
            Version = "$Latest"
        },
        MinSize = Variables.min_size,
        MaxSize = Variables.max_size,
        DesiredCapacity = Variables.desired_capacity,
        HealthCheckType = "EC2",
        HealthCheckGracePeriod = 300,
        Tags = asgTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // Load Balancer
    var loadBalancer = new LoadBalancer("webserver-lb", new LoadBalancerArgs
    {
        SecurityGroups = new[] { securityGroup.Id },
        Subnets = getSubnets.Apply(s => s.Ids),
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // Target Group
    var targetGroup = new TargetGroup("webserver-target-group", new TargetGroupArgs
    {
        Port = 80,
        Protocol = "HTTP",
        VpcId = vpcId,
        Tags = Variables.DefaultTags
    }, new CustomResourceOptions { Provider = awsProvider });

    // TLS Certificate
    var certificate = Variables.create_dns_record ?
        new Certificate("webserver-certificate", new CertificateArgs
        {
            DomainName = Variables.dns_name,
            ValidationMethod = "DNS",
            Tags = Variables.DefaultTags
        }, new CustomResourceOptions { Provider = awsProvider }) : null;

    var zoneId = System.Environment.GetEnvironmentVariable("ROUTE53_ZONE_ID") ??
        throw new Exception("ROUTE53_ZONE_ID environment variable is required");

    // DNS record creation
    var dnsRecord = Variables.create_dns_record ?
        new Record("dns-record", new RecordArgs
        {
            Name = Variables.dns_name,
            Type = "A",
            ZoneId = zoneId,
            Aliases = new RecordAliasArgs[]
            {
                new RecordAliasArgs
                {
                    Name = loadBalancer.DnsName,
                    ZoneId = loadBalancer.ZoneId,
                    EvaluateTargetHealth = true
                }
            }
        }, new CustomResourceOptions { Provider = awsProvider })
        : null;

var certificateValidationRecord = Variables.create_dns_record && certificate != null ?
    certificate.DomainValidationOptions.Apply(options =>
    {
        if (!options.Any())
        {
            throw new Exception("No domain validation options available");
        }
        var option = options[0];
        return new Record("certificate-validation", new RecordArgs
        {
            Name = option.ResourceRecordName!,
            Type = option.ResourceRecordType!,
            ZoneId = zoneId,
            Records = new[] { option.ResourceRecordValue! },
            Ttl = 60
        }, new CustomResourceOptions { Provider = awsProvider });
    })  
    : null;




    // HTTPS Listener
    var httpsListener = Variables.create_dns_record && certificate != null ?
        new Listener("https-listener", new ListenerArgs
        {
            LoadBalancerArn = loadBalancer.Arn,
            Port = 443,
            Protocol = "HTTPS",
            CertificateArn = certificate.Arn,
            DefaultActions = new ListenerDefaultActionArgs[]
            {
                new ListenerDefaultActionArgs
                {
                    Type = "forward",
                    TargetGroupArn = targetGroup.Arn
                }
            }
        }, new CustomResourceOptions { Provider = awsProvider })
    : null;

    // HTTP Listener
    var httpListener = !Variables.create_dns_record ?
        new Listener("http-listener", new ListenerArgs
        {
            LoadBalancerArn = loadBalancer.Arn,
            Port = 80,
            Protocol = "HTTP",
            DefaultActions = new ListenerDefaultActionArgs[]
            {
                new ListenerDefaultActionArgs
                {
                    Type = "forward",
                    TargetGroupArn = targetGroup.Arn
                }
            }
        }, new CustomResourceOptions { Provider = awsProvider })
        : null;

    // HTTP to HTTPS Redirect Listener
    var httpRedirectListener = Variables.create_dns_record && certificate != null ?
        new Listener("http-redirect-listener", new ListenerArgs
        {
            LoadBalancerArn = loadBalancer.Arn,
            Port = 80,
            Protocol = "HTTP",
            DefaultActions = new ListenerDefaultActionArgs[]
            {
                new ListenerDefaultActionArgs
                {
                    Type = "redirect",
                    Redirect = new ListenerDefaultActionRedirectArgs
                    {
                        Protocol = "HTTPS",
                        Port = "443",
                        StatusCode = "HTTP_301"
                    }
                }
            }
        }, new CustomResourceOptions { Provider = awsProvider })
        : null;

    // Attach the ASG to the Load Balancer
    var asgAttachment = new Attachment("asg-attachment", new AttachmentArgs
    {
        AutoscalingGroupName = autoScalingGroup.Name,
        LbTargetGroupArn = targetGroup.Arn
    }, new CustomResourceOptions { Provider = awsProvider });

    // Exports
    return new Dictionary<string, object?>
    {
        ["loadBalancerDns"] = loadBalancer.DnsName,
        ["dnsRecord"] = Variables.create_dns_record ? dnsRecord?.Fqdn : null,
        ["ssmEndpointId"] = ssmEndpoint.Id,
        ["ec2MessagesEndpointId"] = ec2MessagesEndpoint.Id,
        ["ssmmessagesEndpointId"] = ssmmessagesEndpoint.Id
    };
});