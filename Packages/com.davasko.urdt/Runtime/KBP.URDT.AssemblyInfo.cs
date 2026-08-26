using System.Runtime.CompilerServices;

// Exposes internal test seams (e.g. MainThreadDispatcher.ClearInstanceForTests) to
// the URDT test assembly without widening the public API.
[assembly: InternalsVisibleTo("KBP.URDT.Tests")]
