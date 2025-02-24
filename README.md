# Pulumi C# AWS Auto Scaling Group Webserver

This Pulumi infrastructure as code (IaC) project deploys a scalable web application architecture in AWS using C#. The stack creates an Auto Scaling Group of EC2 instances serving a simple "Hello World" page behind a Load Balancer with optional TLS and DNS configuration.

This implementation is a C# port of:
- [TypeScript version](https://github.com/joshuamkite/pulumi-typescript-aws-asg-ec2-webserver)
- [Python version](https://github.com/joshuamkite/pulumi-aws-asg)


# Contents

- [Pulumi C# AWS Auto Scaling Group Webserver](#pulumi-c-aws-auto-scaling-group-webserver)
- [Contents](#contents)
  - [Architecture](#architecture)
  - [Prerequisites](#prerequisites)
  - [NuGet Packages](#nuget-packages)
  - [Environment Setup](#environment-setup)
  - [Configuration](#configuration)
  - [Deployment](#deployment)
  - [Features](#features)
    - [SSM Access Without SSH](#ssm-access-without-ssh)
    - [VPC Endpoints](#vpc-endpoints)
    - [HTTPS Support](#https-support)
  - [Outputs](#outputs)
  - [Resources list](#resources-list)


## Architecture

This stack deploys:

- Amazon EC2 instances in an Auto Scaling Group
- Application Load Balancer with HTTP/HTTPS listeners
- Security Groups for both the load balancer and EC2 instances
- IAM roles for EC2 with SSM access
- VPC Endpoints for AWS Systems Manager (SSM) connectivity
- Optional Route53 DNS and ACM certificate configuration

## Prerequisites

- [Pulumi CLI](https://www.pulumi.com/docs/get-started/install/)
- [.NET SDK](https://dotnet.microsoft.com/download)
- AWS credentials configured
- An existing VPC
- An existing Route53 Hosted Zone (if using DNS features)

## NuGet Packages

This project requires the following NuGet packages:

```bash
dotnet add package Pulumi
dotnet add package Pulumi.Aws
dotnet add package Newtonsoft.Json
```

In your project file (`.csproj`), you should include:

```xml
<ItemGroup>
  <PackageReference Include="Pulumi" Version="3.*" />
  <PackageReference Include="Pulumi.Aws" Version="5.*" />
  <PackageReference Include="Newtonsoft.Json" Version="13.*" />
</ItemGroup>
```

## Environment Setup

This project requires environment variables to be set for the VPC and Route53 Hosted Zone:

```bash
# Required environment variables
export VPC_ID="your-vpc-id"
export ROUTE53_ZONE_ID="your-route53-zone-id"

# Optional AWS configuration
export AWS_PROFILE="your-profile"
export AWS_REGION="eu-west-1"
```

You can create a `.env` file with these variables and source it before running Pulumi commands:

```bash
source .env
pulumi up
```

## Configuration

The stack uses the `Variables.cs` file for configuration Please update before use.

## Deployment

1. Clone this repository
2. Set the required environment variables:
   ```bash
   export VPC_ID="your-vpc-id"
   export ROUTE53_ZONE_ID="your-route53-zone-id"
   ```
3. Initialize a new Pulumi stack:
   ```bash
   pulumi stack init dev
   ```
4. Deploy the stack:
   ```bash
   pulumi up
   ```
5. To destroy the resources when finished:
   ```bash
   pulumi destroy
   ```

You can also use Pulumi state storage backends like S3 by configuring the login before deployment:
```bash
pulumi login "s3://your-bucket-name/path?region=your-region"
```

## Features

### SSM Access Without SSH

This project configures instances to be managed via AWS Systems Manager (SSM) Session Manager. No SSH keys or bastion hosts are required.

To connect to an instance:

```bash
aws ssm start-session --target i-xxxxxxxxxxxxxxxxx
```

### VPC Endpoints

The project creates VPC Endpoints for SSM, EC2 Messages, and SSM Messages to allow instances to communicate with AWS services without requiring internet access. This eliminates the need for NAT Gateways, reducing costs and improving security.

### HTTPS Support

When `create_dns_record` is set to `true`, the load balancer is configured with HTTPS support using an ACM certificate. HTTP requests are automatically redirected to HTTPS.

## Outputs

- `loadBalancerDns` - DNS name of the load balancer
- `dnsRecord` - The configured domain name (if DNS is enabled)
- `ssmEndpointId` - ID of the SSM VPC Endpoint
- `ec2MessagesEndpointId` - ID of the EC2 Messages VPC Endpoint
- `ssmmessagesEndpointId` - ID of the SSM Messages VPC Endpoint

## Resources list

via `pulumi stack`

```bash

     Type                             Name                                                     Plan       
 +   pulumi:pulumi:Stack              pulumi-csharp-aws-asg-ec2-webserver-newstack-2025-02-24  create     
 +   ├─ pulumi:providers:aws          aws                                                      create     
 +   ├─ aws:ec2:SecurityGroup         instance-security-group                                  create     
 +   ├─ aws:iam:Role                  ec2-role                                                 create     
 +   ├─ aws:acm:Certificate           webserver-certificate                                    create     
 +   ├─ aws:ec2:SecurityGroup         lb-security-group                                        create     
 +   ├─ aws:alb:TargetGroup           webserver-target-group                                   create     
 +   ├─ aws:ec2:SecurityGroupRule     instance-egress                                          create     
 +   ├─ aws:ec2:SecurityGroupRule     instance-http-inbound                                    create     
 +   ├─ aws:ec2:SecurityGroupRule     egress                                                   create     
 +   ├─ aws:iam:InstanceProfile       ec2-instance-profile                                     create     
 +   ├─ aws:ec2:SecurityGroupRule     http-inbound                                             create     
 +   ├─ aws:iam:RolePolicyAttachment  ssm-core-policy-attachment                               create     
 +   ├─ aws:ec2:SecurityGroupRule     https-inbound                                            create     
 +   ├─ aws:ec2:VpcEndpoint           ec2messages-endpoint                                     create     
 +   ├─ aws:alb:LoadBalancer          webserver-lb                                             create     
 +   ├─ aws:route53:Record            dns-record                                               create     
 +   ├─ aws:alb:Listener              http-redirect-listener                                   create     
 +   ├─ aws:alb:Listener              https-listener                                           create     
 +   ├─ aws:ec2:VpcEndpoint           ssm-endpoint                                             create     
 +   ├─ aws:ec2:VpcEndpoint           ssmmessages-endpoint                                     create     
 +   ├─ aws:ec2:LaunchTemplate        ec2-launch-template                                      create     
 +   ├─ aws:autoscaling:Group         webserver-asg                                            create     
 +   └─ aws:autoscaling:Attachment    asg-attachment                                           create     

Outputs:
    dnsRecord            : output<string>
    ec2MessagesEndpointId: output<string>
    loadBalancerDns      : output<string>
    ssmEndpointId        : output<string>
    ssmmessagesEndpointId: output<string>

Resources:
    + 24 to create

```
