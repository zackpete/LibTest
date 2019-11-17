using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Xunit;

namespace LibTest {

public class TestClass {

  [Fact]
  public void Verify() {
    var lib = Implement<ILibrary>("../../../libexample.so");

    Assert.Equal(4, lib.Add(2, 2));

    var result = 1;
    lib.AddOne(ref result);
    Assert.Equal(2, result);
    lib.AddOne(ref result);
    Assert.Equal(3, result);

    lib.OutputOne(out int a);
    Assert.Equal(1, a);
  }

  public interface ILibrary {
    [EntryPoint("add")]
    int Add(int a, int b);

    [EntryPoint("add_one")]
    void AddOne(ref int a);

    [EntryPoint("add_one")]
    void OutputOne(out int a);
  }

  public static T Implement<T>(string name) {
    Type iface = typeof(T);

    var assemblyName = new AssemblyName($"Generated{nameof(T)}");

    AssemblyBuilder assemblyBuilder =
      AssemblyBuilder.DefineDynamicAssembly(
        assemblyName,
        AssemblyBuilderAccess.Run
      );

    ModuleBuilder moduleBuilder =
      assemblyBuilder.DefineDynamicModule(assemblyName.Name);

    TypeBuilder typeBuilder =
      moduleBuilder.DefineType(
        "GeneratedObject",
        TypeAttributes.Public | TypeAttributes.Class
      );

    typeBuilder.AddInterfaceImplementation(typeof(T));

    foreach (MethodInfo method in iface.GetMethods()) {
      Type[] ptypes = method.GetParameters()
                            .Select(e => e.ParameterType)
                            .ToArray();

      var entrypoint = method.GetAttribute<EntryPoint>();

      MethodBuilder externBuilder = typeBuilder.DefinePInvokeMethod(
        $"Extern{method.Name}",
        name,
        entrypoint.Name,
        MethodAttributes.Private |
        MethodAttributes.HideBySig |
        MethodAttributes.Static |
        MethodAttributes.PinvokeImpl,
        CallingConventions.Standard,
        method.ReturnType,
        null,
        null,
        ptypes,
        null,
        null,
        CallingConvention.Cdecl,
        CharSet.Ansi
      );

      externBuilder.SetImplementationFlags(MethodImplAttributes.PreserveSig);

      MethodBuilder implBuilder = typeBuilder.DefineMethod(
        method.Name,
        MethodAttributes.Public |
        MethodAttributes.Final |
        MethodAttributes.HideBySig |
        MethodAttributes.Virtual |
        MethodAttributes.NewSlot,
        CallingConventions.Standard,
        method.ReturnType,
        null,
        null,
        ptypes,
        null,
        null
      );

      ILGenerator generator = implBuilder.GetILGenerator();

      for (var i = 1; i <= ptypes.Length; i++) {
        generator.Emit(OpCodes.Ldarg, i);
      }

      generator.EmitCall(OpCodes.Call, externBuilder, null);
      generator.Emit(OpCodes.Ret);

      typeBuilder.DefineMethodOverride(implBuilder, method);
    }

    Type generatedType = typeBuilder.CreateType();
    return (T) Activator.CreateInstance(generatedType);
  }
}

[AttributeUsage(AttributeTargets.Method)]
public class EntryPoint : Attribute {

  public EntryPoint(string name) {
    Name = name;
  }

  public string Name { get; }
}

public static class Util {
  public static T GetAttribute<T>(this MethodInfo info) where T : Attribute {
    return (T) Attribute.GetCustomAttribute(info, typeof(T));
  }
}

}