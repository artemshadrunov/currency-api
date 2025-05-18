using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Hosting;
using Amazon.Lambda.Core;
using CurrencyConverter.Core; // Для Startup

namespace CurrencyConverter.Core;

public class LambdaEntryPoint : APIGatewayProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.UseStartup<Startup>();
    }

    public override Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
    {
        return base.FunctionHandlerAsync(request, lambdaContext);
    }
}