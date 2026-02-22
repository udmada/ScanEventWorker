using Amazon.CDK;
using ScanEventWorker.Cdk;
using Environment = Amazon.CDK.Environment;

var app = new App();
_ = new ScanEventWorkerStack(app, "ScanEventWorkerStack",
    new StackProps { Env = new Environment { Region = "ap-southeast-2" } });
app.Synth();
