#!/bin/sh
set -e

awslocal s3 mb s3://yotei-artifacts || true
awslocal sqs create-queue --queue-name yotei-analysis || true
