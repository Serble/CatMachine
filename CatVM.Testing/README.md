# CatVM Testing
Correctness of the VM is extemely important so the aim is 100% useful test coverage on the instruction
executors at the very least, and any core VM run logic.

## Test Evaluation
In order to achieve better quality test coverage we use Stryker to perform mutation testing. Except for a few
exceptions there should be **zero** surviving mutants in instruction executors. The exception is that some
of the mutations that Strkyer performs genuinely do not change the behaviour. For example, it likes to
swap `>>` for `>>>` (signed shift), but this doesn't do anything because we're working with uints.

Install the tool with 
```sh
dotnet tool install -g dotnet-stryker
```

and then start a test.
```sh
# ensure we're in the CatVM.Testing directory
cd CatVM.Testing

# start the test
dotnet stryker -p CatVM.csproj
```
