using System.Collections.Generic;

public static class Variables
{
    // AWS configuration
    public const string aws_region = "eu-west-1";

    // EC2 Configuration
    public const string instance_type = "t2.micro";  // Fixed: removed string concatenation

    public static int min_size => EC2Config.min_size;
    public static int max_size => EC2Config.max_size;
    public static int desired_capacity => EC2Config.desired_capacity;

    private static class EC2Config
    {
        public const int min_size = 1;
        public const int max_size = 3;
        public const int desired_capacity = 1;
    }

    // DefaultTags should be a Dictionary<string, string>
    public static Dictionary<string, string> DefaultTags = new()
    {
        ["project"] = "pulumi-aws-ec2-asg",
        ["owner"] = "Joshua",
        ["Name"] = "pulumi-aws-ec2-asg"
    };

    public const bool create_dns_record = true;
    public const string dns_name = "ec2-asg.pulumi.joshuakite.co.uk";
    public const string domain_name = "joshuakite.co.uk";
}