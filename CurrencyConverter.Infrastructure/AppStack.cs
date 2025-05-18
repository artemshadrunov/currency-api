using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace CurrencyConverter.Infrastructure;

public class AppStack : Stack
{
    internal AppStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        // Create VPC
        var vpc = new Vpc(this, "CurrencyConverterVpc", new VpcProps
        {
            MaxAzs = 2,
            NatGateways = 1,
            SubnetConfiguration = new[]
            {
                new SubnetConfiguration
                {
                    Name = "Public",
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                },
                new SubnetConfiguration
                {
                    Name = "Private",
                    SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                    CidrMask = 24
                }
            }
        });

        // Create Redis security group
        var redisSecurityGroup = new SecurityGroup(this, "RedisSecurityGroup", new SecurityGroupProps
        {
            Vpc = vpc,
            Description = "Security group for Redis ElastiCache",
            AllowAllOutbound = true
        });

        // Create Redis cache cluster
        var redisCache = new CfnCacheCluster(this, "RedisCache", new CfnCacheClusterProps
        {
            Engine = "redis",
            CacheNodeType = "cache.t3.micro",
            NumCacheNodes = 1,
            Port = 6379,
            VpcSecurityGroupIds = new[] { redisSecurityGroup.SecurityGroupId },
            CacheSubnetGroupName = new CfnSubnetGroup(this, "RedisSubnetGroup", new CfnSubnetGroupProps
            {
                Description = "Subnet group for Redis ElastiCache",
                SubnetIds = vpc.PrivateSubnets.Select(s => s.SubnetId).ToArray()
            }).Ref
        });

        // Create Lambda security group
        var lambdaSecurityGroup = new SecurityGroup(this, "LambdaSecurityGroup", new SecurityGroupProps
        {
            Vpc = vpc,
            Description = "Security group for Lambda function",
            AllowAllOutbound = true
        });

        // Allow Lambda to access Redis
        redisSecurityGroup.AddIngressRule(
            lambdaSecurityGroup,
            Port.Tcp(6379),
            "Allow Lambda to access Redis"
        );

        // Create Lambda function
        var lambdaFunction = new Function(this, "CurrencyConverterFunction", new FunctionProps
        {
            Runtime = new Runtime("dotnet8"),
            Handler = "CurrencyConverter.Core::CurrencyConverter.Core.LambdaEntryPoint::FunctionHandlerAsync",
            Code = Code.FromAsset("../CurrencyConverter.Core/bin/Debug/net8.0"),
            Vpc = vpc,
            SecurityGroups = new[] { lambdaSecurityGroup },
            Environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Redis__ConnectionString"] = $"{redisCache.AttrRedisEndpointAddress}:{redisCache.AttrRedisEndpointPort}"
            },
            MemorySize = 512,
            Timeout = Duration.Seconds(30)
        });

        // Create API Gateway
        var api = new RestApi(this, "CurrencyConverterApi", new RestApiProps
        {
            RestApiName = "Currency Converter API",
            Description = "API for currency conversion"
        });

        // Add proxy integration
        var integration = new LambdaIntegration(lambdaFunction);
        api.Root.AddProxy(new ProxyResourceOptions
        {
            DefaultIntegration = integration
        });
    }
}