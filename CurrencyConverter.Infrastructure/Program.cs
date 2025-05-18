using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;

namespace CurrencyConverter.Infrastructure;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new App();
        new AppStack(app, "CurrencyConverterStack", new StackProps());
        app.Synth();
    }
}