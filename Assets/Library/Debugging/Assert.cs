using System.Collections.Generic;
using UnityAssert = UnityEngine.Assertions.Assert;

// Project-wide façade so unqualified Assert.* binds to Unity assertions
// instead of NUnit when Unity's test framework is referenced.
public static class Assert
{
    public static void IsTrue(bool condition)
    {
        UnityAssert.IsTrue(condition);
    }

    public static void IsTrue(bool condition, string message)
    {
        UnityAssert.IsTrue(condition, message);
    }

    public static void IsFalse(bool condition)
    {
        UnityAssert.IsFalse(condition);
    }

    public static void IsFalse(bool condition, string message)
    {
        UnityAssert.IsFalse(condition, message);
    }

    public static void AreEqual<T>(T expected, T actual)
    {
        UnityAssert.AreEqual(expected, actual);
    }

    public static void AreEqual<T>(T expected, T actual, string message)
    {
        UnityAssert.AreEqual(expected, actual, message);
    }

    public static void AreEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
    {
        UnityAssert.AreEqual(expected, actual, message, comparer);
    }

    public static void AreNotEqual<T>(T expected, T actual)
    {
        UnityAssert.AreNotEqual(expected, actual);
    }

    public static void AreNotEqual<T>(T expected, T actual, string message)
    {
        UnityAssert.AreNotEqual(expected, actual, message);
    }

    public static void AreNotEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
    {
        UnityAssert.AreNotEqual(expected, actual, message, comparer);
    }

    public static void AreApproximatelyEqual(float expected, float actual)
    {
        UnityAssert.AreApproximatelyEqual(expected, actual);
    }

    public static void AreApproximatelyEqual(float expected, float actual, float tolerance)
    {
        UnityAssert.AreApproximatelyEqual(expected, actual, tolerance);
    }

    public static void AreApproximatelyEqual(float expected, float actual, string message)
    {
        UnityAssert.AreApproximatelyEqual(expected, actual, message);
    }

    public static void AreApproximatelyEqual(float expected, float actual, float tolerance, string message)
    {
        UnityAssert.AreApproximatelyEqual(expected, actual, tolerance, message);
    }

    public static void AreNotApproximatelyEqual(float expected, float actual)
    {
        UnityAssert.AreNotApproximatelyEqual(expected, actual);
    }

    public static void AreNotApproximatelyEqual(float expected, float actual, string message)
    {
        UnityAssert.AreNotApproximatelyEqual(expected, actual, message);
    }

    public static void AreNotApproximatelyEqual(float expected, float actual, float tolerance)
    {
        UnityAssert.AreNotApproximatelyEqual(expected, actual, tolerance);
    }

    public static void AreNotApproximatelyEqual(float expected, float actual, float tolerance, string message)
    {
        UnityAssert.AreNotApproximatelyEqual(expected, actual, tolerance, message);
    }

    public static void IsNull(UnityEngine.Object value)
    {
        UnityAssert.IsNull(value);
    }

    public static void IsNull(UnityEngine.Object value, string message)
    {
        UnityAssert.IsNull(value, message);
    }

    public static void IsNull<T>(T value) where T : class
    {
        UnityAssert.IsNull(value);
    }

    public static void IsNull<T>(T value, string message) where T : class
    {
        UnityAssert.IsNull(value, message);
    }

    public static void IsNotNull(UnityEngine.Object value)
    {
        UnityAssert.IsNotNull(value);
    }

    public static void IsNotNull(UnityEngine.Object value, string message)
    {
        UnityAssert.IsNotNull(value, message);
    }

    public static void IsNotNull<T>(T value) where T : class
    {
        UnityAssert.IsNotNull(value);
    }

    public static void IsNotNull<T>(T value, string message) where T : class
    {
        UnityAssert.IsNotNull(value, message);
    }
}
