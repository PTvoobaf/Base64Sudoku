using System;
using System.Reflection;
using Xunit.Sdk;

public class LogStartAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
    {
        Console.WriteLine(
            $"[STARTING] {methodUnderTest.DeclaringType?.FullName}.{methodUnderTest.Name} " +
            $"@ {DateTime.Now:HH:mm:ss.fff}");
    }
}