#!/bin/bash
set -e

echo "Creating SQS queues..."

awslocal sqs create-queue --queue-name scan-events-dlq

awslocal sqs create-queue --queue-name scan-events-queue \
  --attributes '{"RedrivePolicy":"{\"deadLetterTargetArn\":\"arn:aws:sqs:ap-southeast-2:000000000000:scan-events-dlq\",\"maxReceiveCount\":\"3\"}"}'

echo "SQS queues created successfully"
