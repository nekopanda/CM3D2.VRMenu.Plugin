using Mono.Cecil;
using System.IO;
using System.Linq;

namespace VRMenuPluginPatcher
{
    public static class VRMenuPluginPatcher
    {
        public static readonly string[] TargetAssemblyNames = new string[]
           {
            "Assembly-CSharp.dll"
           };

        public static void Patch(AssemblyDefinition assembly)
        {
            // VR版かどうか確認
            var typeVR = assembly.MainModule.GetType("SteamVR_TrackedObject");
            if (typeVR == null)
            {
                return;
            }

            // GameMain.Update()のカーソル非表示ロジックを無効化 //

            var typeGameMain = assembly.MainModule.GetType("GameMain");
            var updateMethod = typeGameMain.Methods.FirstOrDefault(method => method.Name == "Update");
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
                                //assembly.Write("vrmemu-mod.dll");
                                break;
                            }
                        }
                    }
                }
            }
        }

    }
}
