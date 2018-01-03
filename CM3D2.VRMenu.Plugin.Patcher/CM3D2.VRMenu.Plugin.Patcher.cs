using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CM3D2.VRMenu.Plugin.Patcher
{
    public static class VRMenuPluginPatcher
    {
        public static readonly string[] TargetAssemblyNames = new string[]
           {
            "Assembly-CSharp.dll"
           };

        public static void Patch(AssemblyDefinition assembly)
        {
            // プラグインフィルタ
            //string[] supportExeNames = new string[] {
            //    "CM3D2VRx64", "CM3D2OHVRx64"
            //};
            //if (!supportExeNames.Contains(Process.GetCurrentProcess().ProcessName))
            //{
            //    return;
            //}

            Patch_(assembly);
        }

        static void Main(string[] args)
        {
            // テスト及びManaged開発用モジュール作成
            string path = @"T:\sandbox\t31\CM3D2Work\work\Assembly-CSharp.dll";
            var assembly = AssemblyDefinition.ReadAssembly(path);

            Patch_(assembly);

            assembly.Write("Assembly-CSharp-mod.dll");
            Console.WriteLine("完了");
            Console.ReadLine();
        }

        public static void Patch_(AssemblyDefinition assembly)
        {
            // GameMain.Update()のカーソル非表示ロジックを無効化 //

            var typeGameMain = assembly.MainModule.GetType("GameMain");
            var updateMethod = typeGameMain.Methods.FirstOrDefault(method => method.Name == "Update" || method.Name == "Update_orig");
            if (updateMethod != null)
            {
                var processor = updateMethod.Body.GetILProcessor();
                foreach (var inst in updateMethod.Body.Instructions)
                {
                    // call Input.GetMouseButtonUpを消して引数の0を分岐brfalseにそのまま渡す
                    if (inst.OpCode.Name == "call")
                    {
                        var target = inst.Operand as MethodReference;
                        if (target != null)
                        {
                            if (target.Name == "GetMouseButtonUp")
                            {
                                processor.Remove(inst);
                                break;
                            }
                        }
                    }
                }
            }
            /*

            // カメラ位置を修正 //
            try
            {
                var typeViveCamera = assembly.MainModule.GetType("ViveCamera");

                var trOffsetField = typeViveCamera.Fields.First(f => f.Name == "m_trOffset");
                trOffsetField.IsPublic = true;

                // フック
                string dirPath = Path.GetDirectoryName(typeof(VRMenuPluginPatcher).Assembly.Location);
                var calleeAssembly = AssemblyDefinition.ReadAssembly(dirPath + "\\CM3D2.VRMenu.Plugin.Managed.dll");
                var calleeType = calleeAssembly.MainModule.GetType("CM3D2.VRMenu.Plugin.Managed.ViveCameraDelegate");

                HookSetPos(assembly.MainModule, typeViveCamera, calleeType);
                HookGetPos(assembly.MainModule, typeViveCamera, calleeType);
            }
            catch (Exception e)
            {
                Console.WriteLine("failed: " + e.Message);
                Console.WriteLine(e.StackTrace);
            }
            */
        }

        public static void HookSetPos(ModuleDefinition targetModule,
            TypeDefinition targetType, TypeDefinition calleeType)
        {
            var calleeMethod = calleeType.Methods.First(m => m.Name == "SetPos");
            var method = targetType.Methods.First(m => m.Name == "SetPos");
            var processor = method.Body.GetILProcessor();

            // ldarg.1の後にldarg.0,call SetPosを追加
            var next = method.Body.Instructions.First(i => i.OpCode == OpCodes.Ldarg_1).Next;
            processor.InsertBefore(next, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(next, processor.Create(OpCodes.Call, targetModule.ImportReference(calleeMethod)));
        }

        public static void HookGetPos(ModuleDefinition targetModule,
            TypeDefinition targetType, TypeDefinition calleeType)
        {
            var calleeMethod = calleeType.Methods.First(m => m.Name == "GetPos");
            var method = targetType.Methods.First(m => m.Name == "GetPos");
            var processor = method.Body.GetILProcessor();

            // retの前にldarg.0,call GetPosを追加
            var ldarg1 = method.Body.Instructions.First(i => i.OpCode == OpCodes.Ret);
            processor.InsertBefore(ldarg1, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(ldarg1, processor.Create(OpCodes.Call, targetModule.ImportReference(calleeMethod)));
        }

    }
}
