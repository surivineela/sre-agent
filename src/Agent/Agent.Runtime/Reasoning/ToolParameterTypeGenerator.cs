// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Reflection.Emit;
using Agent.Framework;

public static class ToolParameterTypeGenerator
{
    public static Type Create(YamlToolDefinitionBase tool)
    {
        var asmName = new AssemblyName("DynamicYamlToolTypes");
        var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        var module = asmBuilder.DefineDynamicModule("Main");

        var typeBuilder = module.DefineType($"ToolArgs_{tool.Name.Replace(" ", "_")}", TypeAttributes.Public | TypeAttributes.Class);

        foreach (var param in tool.Parameters)
        {
            var type = param.Type switch
            {
                "int" => typeof(int),
                "bool" => typeof(bool),
                "double" => typeof(double),
                _ => typeof(string)
            };

            var field = typeBuilder.DefineField($"_{param.Name}", type, FieldAttributes.Private);
            var prop = typeBuilder.DefineProperty(param.Name, PropertyAttributes.None, type, null);

            var getter = typeBuilder.DefineMethod($"get_{param.Name}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                type, Type.EmptyTypes);
            var gIL = getter.GetILGenerator();
            gIL.Emit(OpCodes.Ldarg_0);
            gIL.Emit(OpCodes.Ldfld, field);
            gIL.Emit(OpCodes.Ret);
            prop.SetGetMethod(getter);

            var setter = typeBuilder.DefineMethod($"set_{param.Name}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null, new[] { type });
            var sIL = setter.GetILGenerator();
            sIL.Emit(OpCodes.Ldarg_0);
            sIL.Emit(OpCodes.Ldarg_1);
            sIL.Emit(OpCodes.Stfld, field);
            sIL.Emit(OpCodes.Ret);
            prop.SetSetMethod(setter);
        }

        return typeBuilder.CreateType()!;
    }
}
