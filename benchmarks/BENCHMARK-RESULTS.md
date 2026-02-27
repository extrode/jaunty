# Jaunty Benchmark Results

**BenchmarkDotNet v0.14.0** | Windows 11 | AMD Ryzen 7 7840HS (8C/16T) | .NET 8.0.24 | SQLite in-memory

ORMs compared: **Jaunty** (source-generated), **Dapper** (baseline), **EF Core 9**, **RepoDb**, **linq2db**

Each benchmark runs two jobs:
- **Cold**: Fresh process, empty caches (WarmupCount=0, InvocationCount=1, 3 iterations)
- **Warm**: Caches populated (WarmupCount=5, 10 iterations)

---

## 1. Query Multi-Row (`SELECT * FROM ... LIMIT N`)

### Warm (Steady State)

| Rows    | Jaunty     | Dapper     | EF Core     | RepoDb    | linq2db    |
|--------:|-----------:|-----------:|------------:|----------:|-----------:|
|       1 |   4.52 us  |   6.10 us  |   88.30 us  |  5.34 us  |   6.95 us  |
|      10 |  13.92 us  |  17.99 us  |   98.56 us  | 10.29 us  |  13.56 us  |
|     100 | 102.12 us  | 123.64 us  |  186.52 us  | 56.84 us  |  71.59 us  |
|   1,000 | 870.70 us  | 970.13 us  |  978.82 us  | 433.89 us | 534.49 us  |
|  10,000 |   9.19 ms  |  11.21 ms  |   10.56 ms  |  4.49 ms  |   6.34 ms  |
| 100,000 | 109.79 ms  | 154.27 ms  |  127.29 ms  | 59.18 ms  |  81.10 ms  |

### Warm Speed vs Dapper

| Rows    | Jaunty        | EF Core         | RepoDb         | linq2db        |
|--------:|--------------:|----------------:|---------------:|---------------:|
|       1 | 1.35x faster  | 14.50x slower   | 1.14x faster   | 1.14x slower   |
|      10 | 1.29x faster  |  5.49x slower   | 1.75x faster   | 1.33x faster   |
|     100 | 1.21x faster  |  1.52x slower   | 2.18x faster   | 1.73x faster   |
|   1,000 | 1.11x faster  |  1.01x slower   | 2.24x faster   | 1.82x faster   |
|  10,000 | 1.22x faster  |  1.06x faster   | 2.50x faster   | 1.78x faster   |
| 100,000 | 1.41x faster  |  1.22x faster   | 2.61x faster   | 1.91x faster   |

### Warm Memory vs Dapper

| Rows    | Jaunty       | EF Core         | RepoDb         | linq2db        |
|--------:|-------------:|----------------:|---------------:|---------------:|
|       1 |  2.19x less  | 25.98x more     |  1.38x less    |  1.39x more    |
|      10 |  2.97x less  |  9.07x more     |  2.58x less    |  1.64x less    |
|     100 |  3.51x less  |  1.72x more     |  4.12x less    |  3.68x less    |
|   1,000 |  3.66x less  |  1.37x less     |  4.60x less    |  4.54x less    |
|  10,000 |  3.51x less  |  1.58x less     |  4.37x less    |  4.37x less    |
| 100,000 |  3.60x less  |  1.62x less     |  4.52x less    |  4.52x less    |

### Cold Start

| Rows    | Jaunty      | Dapper      | EF Core         | RepoDb      | linq2db       |
|--------:|------------:|------------:|----------------:|------------:|--------------:|
|       1 |    76 us    |   229 us    |   12,721 us     |    96 us    |    1,113 us   |
|      10 |    85 us    |   126 us    |   13,090 us     |    96 us    |      846 us   |
|     100 |   191 us    |   313 us    |   13,721 us     |   208 us    |      788 us   |
|   1,000 | 1,291 us    | 1,986 us    |   14,055 us     |   773 us    |    1,610 us   |
|  10,000 |  12.1 ms    |  19.7 ms    |    24.2 ms      |   6.7 ms    |     9.9 ms    |
| 100,000 | 146.8 ms    | 193.4 ms    |   168.2 ms      |  75.4 ms    |   112.3 ms    |

---

## 2. QueryFirst (Single Row by PK)

| ORM    | Cold (Median) | Warm     | Warm Memory | vs Dapper Speed | vs Dapper Memory |
|--------|------------:|---------:|------------:|----------------:|-----------------:|
| Jaunty |    51.3 us  |  5.51 us |    1.63 KB  | **1.29x faster**    | **1.82x less**       |
| Dapper |    66.1 us  |  7.06 us |    2.98 KB  | baseline        | baseline         |
| EF Core| 7,626.0 us  | 110.2 us |   66.61 KB  | 15.65x slower   | 22.38x more      |
| RepoDb |    84.2 us  |  7.55 us |    4.56 KB  | 1.07x slower    | 1.53x more       |
| linq2db|   191.2 us  | 16.91 us |    8.70 KB  | 2.40x slower    | 2.92x more       |

---

## 3. QueryScalar (`SELECT COUNT(*)`)

| ORM    | Cold (Median) | Warm     | Warm Memory | vs Dapper Speed | vs Dapper Memory |
|--------|------------:|---------:|------------:|----------------:|-----------------:|
| Jaunty |    20.5 us  |  2.67 us |      792 B  | 1.16x slower    | 1.00x (same)     |
| Dapper |    15.4 us  |  2.31 us |      792 B  | baseline        | baseline         |
| EF Core| 8,070.3 us  | 71.44 us |   61,145 B  | 31.00x slower   | 77.20x more      |
| RepoDb |    32.4 us  |  2.52 us |      832 B  | 1.09x slower    | 1.05x more       |
| linq2db|   159.5 us  |  5.58 us |    3,520 B  | 2.42x slower    | 4.44x more       |

---

## 4. Single Insert

| ORM    | Cold (Median) | Warm     | Warm Memory | vs Dapper Speed | vs Dapper Memory |
|--------|------------:|---------:|------------:|----------------:|-----------------:|
| Jaunty |    38.4 us  | 29.74 us |    3.08 KB  | 1.17x slower    | 1.00x (same)     |
| Dapper |    65.1 us  | 26.00 us |    3.08 KB  | baseline        | baseline         |
| EF Core| 7,363.7 us  | 309.4 us |   72.50 KB  | 12.21x slower   | 23.55x more      |
| RepoDb |    48.4 us  | 40.75 us |    3.86 KB  | 1.61x slower    | 1.25x more       |
| linq2db|    88.0 us  | 144.6 us |    9.42 KB  | 5.71x slower    | 3.06x more       |

---

## 5. Bulk Insert

### Warm Speed

| Batch  | Jaunty     | Dapper Loop | EF Core      | RepoDb       | linq2db     |
|-------:|-----------:|------------:|-------------:|-------------:|------------:|
|    100 |    260 us  |     743 us  |    2,962 us  |    1,477 us  |     237 us  |
|  1,000 |  2,762 us  |   7,635 us  |   32,731 us  |   15,773 us  |   1,815 us  |
| 10,000 | 22,586 us  |  53,536 us  |  143,198 us  |  114,513 us  |  16,460 us  |

### Warm Speed vs Dapper

| Batch  | Jaunty         | EF Core        | RepoDb        | linq2db        |
|-------:|---------------:|---------------:|--------------:|---------------:|
|    100 | **2.86x faster**   | 3.99x slower   | 1.99x slower  | **3.17x faster**   |
|  1,000 | **2.77x faster**   | 4.29x slower   | 2.07x slower  | **4.23x faster**   |
| 10,000 | **2.38x faster**   | 2.68x slower   | 2.14x slower  | **3.26x faster**   |

### Warm Memory vs Dapper

| Batch  | Jaunty       | EF Core         | RepoDb         | linq2db        |
|-------:|-------------:|----------------:|---------------:|---------------:|
|    100 | 3.59x less   | 3.97x more      | 2.13x more     | **5.35x less**     |
|  1,000 | 3.72x less   | 3.76x more      | 2.11x more     | **5.97x less**     |
| 10,000 | 3.67x less   | 3.66x more      | 2.14x more     | **8.64x less**     |

---

## 6. Mapper Comparison (Jaunty Internal)

| Mapper                    | Warm     | Memory   | vs Source-Gen |
|---------------------------|--------:|---------:|---------------|
| Source-generated (IMapped) | 104.2 us | 16.27 KB | baseline      |
| Reflection (Dictionary)    |  96.0 us | 49.78 KB | 3.06x more memory |

---

## 7. Parameter Binding (Jaunty Internal)

| Method                    | Warm     | Memory   | vs Dapper     |
|---------------------------|--------:|---------:|---------------|
| Jaunty named (anon obj)   |  4.98 us |  1.63 KB | **1.34x faster, 1.82x less** |
| Dapper named (anon obj)   |  6.66 us |  2.98 KB | baseline      |

---

## Summary: Jaunty vs Dapper (Warm, Steady State)

| Benchmark          | Speed             | Memory            |
|--------------------|------------------:|------------------:|
| Query 1 row        | **1.35x faster**  | **2.19x less**    |
| Query 10 rows      | **1.29x faster**  | **2.97x less**    |
| Query 100 rows     | **1.21x faster**  | **3.51x less**    |
| Query 1,000 rows   | **1.11x faster**  | **3.66x less**    |
| Query 10,000 rows  | **1.22x faster**  | **3.51x less**    |
| Query 100,000 rows | **1.41x faster**  | **3.60x less**    |
| QueryFirst         | **1.29x faster**  | **1.82x less**    |
| QueryScalar        | 1.16x slower      | same              |
| Single Insert      | 1.17x slower      | same              |
| Bulk Insert 100    | **2.86x faster**  | **3.59x less**    |
| Bulk Insert 1,000  | **2.77x faster**  | **3.72x less**    |
| Bulk Insert 10,000 | **2.38x faster**  | **3.67x less**    |

### Key Takeaways

1. **Query operations**: Jaunty is 1.1x-1.4x faster than Dapper and uses 2-3.6x less memory across all row counts
2. **Memory scales linearly**: Jaunty's ~3.5x memory advantage holds from 1 row to 100,000 rows
3. **Bulk insert**: Jaunty's transaction-wrapped BulkInsert is 2.4-2.9x faster than Dapper's loop insert
4. **Cold start**: Jaunty's source-generated caches have minimal first-call overhead - competitive with or faster than Dapper
5. **EF Core**: 12-31x slower than Dapper for simple operations, 15-77x more memory. Narrows at very high row counts
6. **RepoDb**: Fastest at multi-row queries (2.2-2.6x faster than Dapper) but slower on inserts
7. **linq2db**: Excellent bulk copy performance (3-4x faster than Dapper), but slower on single operations

---

*Raw BenchmarkDotNet reports: `benchmarks/Jaunty.Benchmarks/BenchmarkDotNet.Artifacts/results/`*
