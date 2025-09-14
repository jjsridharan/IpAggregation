```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.22631.5909/23H2/2023Update/SunValley3) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz (Max: 2.79GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.305
  [Host]     : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-YTMLKF : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Runtime=.NET 8.0  Arguments=/nowarn:CS1591  

```
| Method                      | Mean    | Error    | StdDev   | Gen0       | Gen1       | Gen2      | Allocated  |
|---------------------------- |--------:|---------:|---------:|-----------:|-----------:|----------:|-----------:|
| CreateTrieWith2MillionNodes | 3.064 s | 0.0525 s | 0.0491 s | 43000.0000 | 40000.0000 | 1000.0000 | 1014.72 MB |
