# Lambda Resume

An experiment to verify that a lambda function with a provisioned concurrency of
1 can send an SNS notification to reactivate itself and "resume" an operation.
The primary use case is occasional long running operation consisting of many
smaller operations that fit within a standard lambda operating window (15m).