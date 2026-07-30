# CatVM AOT Tests
This project does some quick tests to ensure that the VM does not break when compiled AOT.
To run tests, dotnet publish it so that it is properly compiled and then run the binary.

All tests should pass after any changes, AOT compatibility is also enabled in the VM project so anything
not compatible should generate a build warning.
