using Xunit;

// The WinForms tests share the same Windows desktop, global theme and language,
// registry and plugin registries. Parallel classes can cover each other's HWNDs
// or modify static state while a screen sample or contract assertion is running.
// Core tests remain parallel; only this desktop test assembly is serialized.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
