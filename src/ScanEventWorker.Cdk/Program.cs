using Amazon.CDK;
using ScanEventWorker.Cdk;

var app = new App();
_ = new ScanEventWorkerStack(app, "ScanEventWorkerStack", new StackProps
{
    Env = new Amazon.CDK.Environment
    {
        Region = "ap-southeast-2"
    }
});
app.Synth();
