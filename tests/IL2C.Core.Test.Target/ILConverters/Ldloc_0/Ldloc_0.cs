using System;
using System.Runtime.CompilerServices;

namespace IL2C.ILConverters
{
    [Case(true, "True")]
    [Case(false, "False")]
    [Case(byte.MaxValue, "Byte")]
    [Case(short.MaxValue, "Int16")]
    [Case(int.MaxValue, "Int32")]
    [Case(long.MaxValue, "Int64")]
    [Case(3.14159274f, "Single")]
    [Case(3.1415926535897931, "Double")]
    [Case('A', "Char")]
    [Case("ABC", "String")]
    public static class Ldloc_0
    {
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern bool True();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern bool False();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern byte Byte();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short Int16();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern int Int32();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern long Int64();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern float Single();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern double Double();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern char Char();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string String();
    }
}