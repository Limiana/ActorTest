using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using Dalamud.Interface;
using System.Numerics;
using ImGuiNET;
using static ActorTest.Structs;
using Dalamud.Game.ClientState.Actors.Types.NonPlayer;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Structs;
using System.Diagnostics;
using Dalamud.Game.Command;

namespace ActorTest
{
    unsafe class ActorTest : IDalamudPlugin
    {
        public string Name => "ActorTest";
        DalamudPluginInterface pi;
        bool init = false;
        HashSet<(Position3 pos, string text, bool colored)> ActorSet = new HashSet<(Position3 pos, string text, bool colored)>();
        byte[] hostileMemory;
        byte[] friendlyMemory;
        Dictionary<int, byte> mem;
        //[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        //public delegate int GetIsTargetable(IntPtr gameObjectPtr);
        //public GetIsTargetable getIsTargetable;

        public void Dispose()
        {
            pi.Framework.OnUpdateEvent -= Tick;
            pi.UiBuilder.OnBuildUi -= Draw;
            pi.CommandManager.RemoveHandler("/at");
            pi.Dispose();
        }

        [HandleProcessCorruptedStateExceptions]
        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            pi = pluginInterface;
            pi.Framework.OnUpdateEvent += Tick;
            pi.UiBuilder.OnBuildUi += Draw;
            try
            {
                init = true;
            }
            catch (Exception e)
            {
                pi.Framework.Gui.Chat.Print("ActorTest error: " + e + "\n" + e.Message);
            }
            pi.CommandManager.AddHandler("/at", new CommandInfo(delegate (string cmd, string args)
            {
                if(args == "")
                {
                    pi.Framework.Gui.Chat.Print("==========================");
                    for(int i = 0; i < 0x2C00; i++) 
                    { 
                        if(friendlyMemory[i] != hostileMemory[i])
                        {
                            pi.Framework.Gui.Chat.Print("Mismatch " + Convert.ToString(i, 16) + ": F:"+ Convert.ToString(friendlyMemory[i],16) + " | H:"+ Convert.ToString(hostileMemory[i], 16));
                        }
                    }
                    pi.Framework.Gui.Chat.Print("==========================");
                    return;
                }
                if (args == "init")
                {
                    mem = new Dictionary<int, byte>();
                    for (int i = 0; i < 0x2C00; i++)
                    {
                        mem[i] = *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + i);
                    }
                    pi.Framework.Gui.Chat.Print("Initialized");
                    return;
                }
                if (args == "diff")
                {
                    var num = 0;
                    for (int i = 0; i < 0x2C00; i++)
                    {
                        if (mem.ContainsKey(i) && mem[i] == *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + i))
                        {
                            pi.Framework.Gui.Chat.Print(Convert.ToString(i, 16).ToUpper() + ": " + Convert.ToString(mem[i], 16).ToUpper());
                            num++;
                            mem.Remove(i);
                        }
                    }
                    pi.Framework.Gui.Chat.Print("Eliminated " + num + " matching entries");
                    return;
                }
                if (args == "same")
                {
                    var num = 0;
                    for (int i = 0; i < 0x2C00; i++)
                    {
                        if (mem.ContainsKey(i) && mem[i] != *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + i))
                        {
                            pi.Framework.Gui.Chat.Print(Convert.ToString(i, 16).ToUpper() + ": " + Convert.ToString(mem[i], 16).ToUpper());
                            num++;
                            mem.Remove(i);
                        }
                    }
                    pi.Framework.Gui.Chat.Print("Eliminated " + num + " different entries");
                    return;
                }
                if (args == "print")
                {
                    foreach(var e in mem)
                    {
                        pi.Framework.Gui.Chat.Print(Convert.ToString(e.Key, 16).ToUpper() + ": " + Convert.ToString(e.Value, 16).ToUpper());
                    }
                    return;
                }
                var lst = new byte[0x2C00];
                for(int i = 0;i< lst.Length; i++)
                {
                    lst[i] = *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + i);
                }
                if (args.Trim() == "h")
                {
                    hostileMemory = lst;
                    pi.Framework.Gui.Chat.Print("Hostile memory saved");
                }
                if (args.Trim() == "f")
                {
                    friendlyMemory = lst;
                    pi.Framework.Gui.Chat.Print("Friendly memory saved");
                }
            }));
        }

        [HandleProcessCorruptedStateExceptions]
        private void Tick(Framework framework)
        {
            if (!init) return;
            try
            {
                ActorSet.Clear();
                foreach (var a in pi.ClientState.Actors) 
                {
                    if (!(a is BattleNpc)) continue;
                    var bnpc = (BattleNpc)a;
                    var bnpcKind = bnpc.BattleNpcKind;
                    var oKind = bnpc.ObjectKind;
                    var friendly = *(int*)(a.Address + 0x8E) > 0;
                    IntPtr vtable = *(IntPtr*)(a.Address);
                    IntPtr vfunc = *(IntPtr*)(vtable + 5*8);
                    //getIsTargetable = Marshal.GetDelegateForFunctionPointer<GetIsTargetable>(vfunc);
                    //pi.Framework.Gui.Chat.Print(Convert.ToString((long)vfunc, 16));
                    //pi.Framework.Gui.Chat.Print("B:"+Convert.ToString(*(long*)((long)Process.GetCurrentProcess().MainModule.BaseAddress + 0x1416F5E60), 16));
                    ActorSet.Add((a.Position,
                        a.Name
                              // + "\nKind: " + oKind
                              // + "/Subking: " + bnpcKind
                              // + "/0x17D: " + ((BattleChara*)a.Address)->field_0x17D
                              //+ "\nStatus flags: " + bnpc.StatusFlags
                              //+ "\n0x94: " + Convert.ToString(((BattleChara*)a.Address)->Character.GameObject.field_0x94, 2).PadLeft(sizeof(int)*8, '0')
                              //+ "\n0x15B: " + Convert.ToString(((BattleChara*)a.Address)->Character.GameObject.field_0x15B, 2).PadLeft(sizeof(byte)*8, '0')
                              //+ "\nAggroFlags: " + Convert.ToString(((BattleChara*)a.Address)->AggroFlags, 2).PadLeft(sizeof(int) *8, '0')
                              //+ "\nCombatFlags: " + Convert.ToString(((BattleChara*)a.Address)->CombatFlags, 2).PadLeft(sizeof(int)*8, '0')
                        , GetIsTargetable((GameObject*)a.Address)));
                }
            }
            catch(Exception e)
            {
                pi.Framework.Gui.Chat.Print("ActorTest error: " + e + "\n" + e.Message);
            }

        }

        bool GetIsTargetable(GameObject* obj)
        {
            byte bVar1;

            bVar1 = *(byte*)&obj->field_0x94;
            if ((((bVar1 & 2) != 0) && ((bVar1 & 4) != 0)) &&
               ((((uint)obj->RenderFlags >> 0xb & 1) == 0 || (0x7f < bVar1))))
            {
                return (obj->RenderFlags & 0xffffe7f7U) == 0;
            }
            return false;
        }

        private void Draw()
        {
            var i = 0;
            var somebool = true;
            foreach(var a in ActorSet)
            {
                if (pi.Framework.Gui.WorldToScreen(new SharpDX.Vector3(a.pos.X, a.pos.Z, a.pos.Y), out var screenPos))
                {
                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(screenPos.X, screenPos.Y));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(1, 1));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
                    ImGui.Begin("##" + ++i, ref somebool,
                        ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoTitleBar |
                        ImGuiWindowFlags.NoNav |
                        ImGuiWindowFlags.NoMove | 
                        ImGuiWindowFlags.NoMouseInputs |
                        ImGuiWindowFlags.AlwaysUseWindowPadding);
                    if (a.colored) ImGui.PushStyleColor(ImGuiCol.Text, 0xff00ff00);
                    ImGui.Text(a.text);
                    if (a.colored) ImGui.PopStyleColor();
                    ImGui.End();
                    ImGui.PopStyleVar();
                }
            }
        }
    }
}
