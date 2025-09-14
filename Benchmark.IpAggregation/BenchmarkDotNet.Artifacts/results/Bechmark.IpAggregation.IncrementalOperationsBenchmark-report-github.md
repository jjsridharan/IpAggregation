```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.22631.5909/23H2/2023Update/SunValley3) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.305
  [Host]     : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-JSMOCL : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-DUAGUC : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Runtime=.NET 8.0  InvocationCount=1  MaxIterationCount=16  
UnrollFactor=1  

```
| Method                      | Job        | Arguments      | LaunchCount | WarmupCount | Mean      | Error    | StdDev   | Median   | Allocated |
|---------------------------- |----------- |--------------- |------------ |------------ |----------:|---------:|---------:|---------:|----------:|
| Remove100NodesIncrementally | Job-JSMOCL | /nowarn:CS1591 | Default     | Default     | 109.39 μs | 67.93 μs | 63.54 μs | 77.55 μs |  10.47 KB |
| Remove100NodesIncrementally | Job-DUAGUC | Default        | 1           | 1           |  97.31 μs | 53.74 μs | 50.27 μs | 73.10 μs |  10.47 KB |
