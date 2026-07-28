using System.Diagnostics.CodeAnalysis;

namespace CatVM.AotTest;

/// <summary>
/// A single named test belonging to a group.
/// </summary>
public sealed class TestCase(string group, string name, Action run) {
    public string Group { get; } = group;
    public string Name { get; } = name;
    public Action Run { get; } = run;
}

/// <summary>
/// Minimal, reflection-free test runner suitable for Native AOT. Tests are registered
/// explicitly (no attribute scanning) so the trimmer can never strip them.
/// </summary>
public sealed class TestRunner {
    private readonly List<TestCase> _tests = [];

    public void Add(string group, string name, Action run) => _tests.Add(new TestCase(group, name, run));

    /// <summary>Runs every registered test and returns the number of failures.</summary>
    public int RunAll() {
        int passed = 0;
        List<(TestCase test, string message)> failures = [];
        string? currentGroup = null;

        foreach (TestCase test in _tests) {
            if (test.Group != currentGroup) {
                currentGroup = test.Group;
                Console.WriteLine();
                Console.WriteLine($"── {currentGroup} ──");
            }

            try {
                test.Run();
                passed++;
                Write(ConsoleColor.Green, "  PASS ");
                Console.WriteLine(test.Name);
            }
            catch (Exception ex) {
                string message = ex is AssertException ae ? ae.Message : $"{ex.GetType().Name}: {ex.Message}";
                failures.Add((test, message));
                Write(ConsoleColor.Red, "  FAIL ");
                Console.WriteLine($"{test.Name}");
                Console.WriteLine($"         {message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('═', 40));
        if (failures.Count == 0) {
            Write(ConsoleColor.Green, $"All {passed} tests passed.");
            Console.WriteLine();
        }
        else {
            Write(ConsoleColor.Red, $"{failures.Count} of {_tests.Count} tests FAILED:");
            Console.WriteLine();
            foreach ((TestCase test, string message) in failures) {
                Console.WriteLine($"  {test.Group} / {test.Name}");
                Console.WriteLine($"    {message}");
            }
        }
        Console.WriteLine($"Total: {_tests.Count}, Passed: {passed}, Failed: {failures.Count}");

        return failures.Count;
    }

    private static void Write(ConsoleColor color, string text) {
        ConsoleColor old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = old;
    }
}

/// <summary>Thrown by <see cref="Check"/> helpers when an assertion fails.</summary>
public sealed class AssertException(string message) : Exception(message);

/// <summary>
/// Lightweight assertion helpers. All throw <see cref="AssertException"/> on failure.
/// </summary>
public static class Check {
    public static void True(bool condition, string? message = null) {
        if (!condition) throw new AssertException(message ?? "Expected condition to be true.");
    }

    public static void False(bool condition, string? message = null) {
        if (condition) throw new AssertException(message ?? "Expected condition to be false.");
    }

    public static void Equal<T>(T expected, T actual, string? message = null) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new AssertException(
                $"{message ?? "Values differ"}. Expected <{Format(expected)}>, got <{Format(actual)}>.");
        }
    }

    public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string? message = null) {
        bool equal = expected.Count == actual.Count;
        for (int i = 0; equal && i < expected.Count; i++) {
            equal = EqualityComparer<T>.Default.Equals(expected[i], actual[i]);
        }
        if (!equal) {
            throw new AssertException(
                $"{message ?? "Sequences differ"}. Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }
    }

    public static void Throws<TException>(Action action, string? message = null) where TException : Exception {
        try {
            action();
        }
        catch (TException) {
            return;
        }
        catch (Exception ex) {
            throw new AssertException(
                $"{message ?? "Wrong exception type"}. Expected {typeof(TException).Name}, got {ex.GetType().Name}.");
        }
        throw new AssertException($"{message ?? "No exception thrown"}. Expected {typeof(TException).Name}.");
    }

    private static string Format<T>(T value) => value switch {
        null => "null",
        uint u => $"0x{u:X8}",
        byte b => $"0x{b:X2}",
        ushort s => $"0x{s:X4}",
        _ => value.ToString() ?? "null",
    };
}
