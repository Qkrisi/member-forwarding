# C# member forwarding

This library simplifies complex reflection lines for when the coder can't reference a member of a type (or even the type itself).

This library requires [Harmony](https://github.com/pardeike/Harmony/releases) to work.

.NET version: 3.5

## Usage

Using directive: `MemberForwarding`

To forward members, add the `MemberForward` attribute to either a method or a property.

If you have reference to the type that you wish to forward to, use `MemberForward(Type _type, string name)`

Otherwise, use `MemberForward(string FullTypeName, string name, string AssemblyName = null)`

Then at runtime, call either `MemberForwardControls.ForwardAll(string HarmonyID)` or `MemberForwardControls.ForwardTypes(string HarmonyID, params Type[] types)`

Forwarded methods/variables will call the original members.

```cs
using System.Runtime.CompilerServices;
using MemberForwarding;

namespace MemberForwardingDemo
{
	class Foo
	{
		private static bool OriginalMethod(int arg)
		{
			 return arg == 3;
		}
		
		private static int OriginalVariable;  //This can either be a field or a property
	}

	class Program
	{
		[MemberForward(typeof(Foo), "OriginalMethod")]    //Without reference to the type, use [MemberForward("MemberForwardingDemo.Foo", "ForwardMethod")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool ForwardMethod(int arg) => default;		//Just so the method has a body and the compiler is happy
		
		[MemberForward(typeof(Foo), "OriginalVariable")]
		static int ForwardVariable
		{
			[MethodImpl(MethodImplOptions.NoInlining)] get;
			[MethodImpl(MethodImplOptions.NoInlining)] set;
		}
		
		public static void Main()
		{
			MemberForwardControls.ForwardAll("demo");
		}
	}
}
```

### Object references

To call instance methods, either add an `ObjectReference` attribute to your method that poitns to a static variable which will be used as an object reference, or add a parameter **called `__instance`** at the start of the method, if you're forwarding to a method or create getter and setter methods with instance variables if you're forwarding to a variable

The `ObjectReference` attribute has the same overloads as the `MemberForward` attribute.

```cs
using System.Runtime.CompilerServices;
using MemberForwarding;

namespace MemberForwardingDemo
{
	class Foo
	{
		private bool OriginalMethod(int arg)
		{
			 return OriginalVariable == 3;
		}
		
		private int OriginalVariable;
	}

	class Program
	{
		static Foo Instance;
		
		[MemberForward(typeof(Foo), "OriginalMethod")]
		[ObjectReference(typeof(Program, "Instance"))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool ForwardMethod(int arg) => default;
		
		[MemberForward(typeof(Foo), "OriginalVariable")]
		[ObjectReference(typeof(Program, "Instance"))]
		static int ForwardVariable
		{
			[MethodImpl(MethodImplOptions.NoInlining)] get;
			[MethodImpl(MethodImplOptions.NoInlining)] set;
		}
		
		[MemberForward(typeof(Foo), "OriginalMethod")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool ForwardMethod(object __instance, int arg) => default;
		
		[MemberForward(typeof(Foo), "OriginalVariable")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void set_ForwardVariable(object instance, int value) {}
		
		[MemberForward(typeof(Foo), "OriginalVariable")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int get_ForwardVariable(Foo instance) => default;
		
		public static void Main()
		{
			MemberForwardControls.ForwardAll("demo");
		}
	}
}
```

## Notes

It's recommended to use `[MethodImpl(MethodImplOptions.NoInlining)]` on methods and accessors to make sure compiler doesn't "remove" ([inline](https://en.wikipedia.org/wiki/Inline_expansion)) these methods.

Methods with the `extern` keyword can be used, it might throw some runtime warnings, but it will work. It can be used to avoid declaring a method body.

**To forward `extern` methods, add a `[MethodImpl(MethodImplOptions.InternalCall)]` attribute to the method!**

```cs
using System.Runtime.CompilerServices;
using MemberForwarding;

namespace MemberForwardingDemo
{
	class Foo
	{
		private static bool OriginalMethod(out int arg)
		{
			 arg = 1;
			 return false;
		}
	}

	class Program
	{
		static Foo Instance;
		
		[MemberForward(typeof(Foo), "OriginalMethod")]
		[MethodImpl(MethodImplOptions.InternalCall)]
		static extern bool ForwardMethod(out int arg);
		
		
		public static void Main()
		{
			MemberForwardControls.ForwardAll("demo");
		}
	}
}
```