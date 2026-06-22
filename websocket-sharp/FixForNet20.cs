#define SUPPORT_NET_20

#if SUPPORT_NET_20

using System;
using System.Collections.Generic;
using System.Text;

namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Assembly|AttributeTargets.Class|AttributeTargets.Method)]
	public sealed class ExtensionAttribute : Attribute { }
}

namespace System
{
	public delegate void Action();
	//public delegate void Action<T0>(T0 obj0);
	public delegate void Action<T0, T1>(T0 obj0, T1 obj1);
	public delegate void Action<T0, T1, T2>(T0 obj0, T1 obj1, T2 obj2);
	public delegate void Action<T0, T1, T2, T3>(T0 obj0, T1 obj1, T2 obj2, T3 obj3);

	public delegate void Func();
	public delegate TR Func<TR>();
	public delegate TR Func<T0, TR>(T0 obj0);
	public delegate TR Func<T0, T1, TR>(T0 obj0, T1 obj1);
	public delegate TR Func<T0, T1, T2, TR>(T0 obj0, T1 obj1, T2 obj2);
}

#endif
