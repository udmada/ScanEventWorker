using Amazon.CDK;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace ScanEventWorker.Cdk;

public class ScanEventWorkerStack : Stack
{
    public ScanEventWorkerStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        var dlq = new Queue(this, "ScanEventsDlq", new QueueProps
        {
            QueueName = "scan-events-dlq",
            RetentionPeriod = Duration.Days(14)
        });

        var mainQueue = new Queue(this, "ScanEventsQueue", new QueueProps
        {
            QueueName = "scan-events-queue",
            VisibilityTimeout = Duration.Seconds(30),
            DeadLetterQueue = new DeadLetterQueue
            {
                Queue = dlq,
                MaxReceiveCount = 3
            }
        });

        // Outputs
        _ = new CfnOutput(this, "QueueUrl", new CfnOutputProps
        {
            Value = mainQueue.QueueUrl,
            Description = "SQS Main Queue URL"
        });

        _ = new CfnOutput(this, "DlqUrl", new CfnOutputProps
        {
            Value = dlq.QueueUrl,
            Description = "SQS Dead Letter Queue URL"
        });
    }
}
