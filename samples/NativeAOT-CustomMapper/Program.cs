using System.Data;

using Jaunty;
using Jaunty.Core;

using Microsoft.Data.Sqlite;

using NativeAOT.CustomMapper;

// NativeAOT-CustomMapper: Demonstrates Jaunty with a hand-written mapper via CommandOptions<T>.
// No source generator or reflection needed. Full manual control over how rows become objects.

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// Create schema
using var cmd = connection.CreateCommand();
cmd.CommandText = """
    CREATE TABLE employees (
        employee_id INTEGER PRIMARY KEY AUTOINCREMENT,
        first_name TEXT NOT NULL,
        last_name TEXT NOT NULL,
        title TEXT NOT NULL,
        hire_date TEXT NOT NULL
    );
    INSERT INTO employees (first_name, last_name, title, hire_date)
        VALUES ('Nancy', 'Davolio', 'Sales Representative', '2020-05-01');
    INSERT INTO employees (first_name, last_name, title, hire_date)
        VALUES ('Andrew', 'Fuller', 'Vice President, Sales', '2018-08-14');
    INSERT INTO employees (first_name, last_name, title, hire_date)
        VALUES ('Janet', 'Leverling', 'Sales Representative', '2021-04-01');
    """;
cmd.ExecuteNonQuery();

Console.WriteLine("=== Jaunty NativeAOT Custom Mapper Sample ===");
Console.WriteLine();

// Hand-written mapper function - zero reflection, zero source generation
static Employee MapEmployee(IDataReader reader)
{
    return new Employee
    {
        EmployeeId = reader.GetInt32(reader.GetOrdinal("employee_id")),
        FirstName = reader.GetString(reader.GetOrdinal("first_name")),
        LastName = reader.GetString(reader.GetOrdinal("last_name")),
        Title = reader.GetString(reader.GetOrdinal("title")),
        HireDate = reader.GetString(reader.GetOrdinal("hire_date"))
    };
}

// Query with custom mapper via CommandOptions
var options = CommandOptions<Employee>.WithMapper(MapEmployee);
var employees = connection.Query<Employee>(
    "SELECT employee_id, first_name, last_name, title, hire_date FROM employees",
    options: options);

Console.WriteLine($"Query with custom mapper: {employees.Count} employees");
foreach (var e in employees)
    Console.WriteLine($"  [{e.EmployeeId}] {e.FirstName} {e.LastName} - {e.Title} (hired {e.HireDate})");

// QueryFirst with custom mapper
var boss = connection.QueryFirst<Employee>(
    "SELECT employee_id, first_name, last_name, title, hire_date FROM employees WHERE title LIKE '%Vice%'",
    options: options);
Console.WriteLine($"\nQueryFirst: {boss.FirstName} {boss.LastName} is {boss.Title}");

Console.WriteLine("\nDone.");