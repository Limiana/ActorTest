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
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;

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
        List<(int, byte)> memlist;
        int memcurAddr = 0;
        byte memcurValue = 0;
        int curPos = 0;
        IntPtr GetUIModule;
        int membase = 10;

        bool editingMemory = false;
        IntPtr lockedActor = IntPtr.Zero;
        
        /*delegate long UnkFunc(long a1, uint a2, uint a3, long a4, byte a5, byte a6);
        Hook<UnkFunc> UnkFuncHook;*/

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate byte Character_GetIsTargetable(IntPtr characterPtr);
        Character_GetIsTargetable GetIsTargetable;
        private bool manualMemEdit;
        private string addrStr = "";
        private string valStr = "";

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate long unknownFunction(IntPtr ptr);
        unknownFunction UnknownFunction;
        private bool displayOverlay = true;

        /*[UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate long sub_1407437A0(IntPtr a1, long a2);
        sub_1407437A0 sub_1407437A0Function;
        IntPtr GameObjectManager;*/


        public void Dispose()
        {
            pi.Framework.OnUpdateEvent -= Tick;
            pi.UiBuilder.OnBuildUi -= Draw;
            pi.CommandManager.RemoveHandler("/at");
            /*UnkFuncHook.Disable();
            UnkFuncHook.Dispose();*/
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
                GetUIModule = pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8B C8 48 85 C0 75 2D"); 
                var ptr = pi.TargetModuleScanner.ScanText("F3 0F 10 89 ?? ?? ?? ?? 0F 57 C0 0F 2E C8 7A 05 75 03 32 C0 C3 80 B9");
                pi.Framework.Gui.Chat.Print("Address: " + ptr);
                GetIsTargetable = Marshal.GetDelegateForFunctionPointer<Character_GetIsTargetable>(ptr);
                var ptr2 = pi.TargetModuleScanner.ScanText("48 89 74 24 ?? 57 48 83 EC 20 48 8B 35 ?? ?? ?? ?? 48 8B F9 48 85 F6 75 0D");
                UnknownFunction = Marshal.GetDelegateForFunctionPointer<unknownFunction>(ptr2);
                /*
                var ptr3 = pi.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 20 8B B9");
                sub_1407437A0Function = Marshal.GetDelegateForFunctionPointer<sub_1407437A0>(ptr3);

                GameObjectManager = pi.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D8 48 8B D3 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 8E");

                pi.Framework.Gui.Chat.Print(GameObjectManager.ToString());
                pi.Framework.Gui.Chat.Print(pi.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 0F B6 83").ToString());
                */

                /*var unkptr = pi.TargetModuleScanner.ScanText("48 89 5C 24 ?? 4C 89 4C 24 ?? 48 89 4C 24 ?? 55 56 57 48 81 EC");
                UnkFuncHook = new Hook<UnkFunc>(unkptr, UnkFuncDetour);
                UnkFuncHook.Enable();*/

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
                    displayOverlay = !displayOverlay;
                    return;
                }
                if(args == "mismatch")
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
                if(args == "manual")
                {
                    manualMemEdit = true;
                    return;
                }
                if(args == "writediff")
                {
                    foreach (var e in mem)
                    {
                        var addr = pi.ClientState.Targets.CurrentTarget.Address;
                        Marshal.WriteByte(addr, e.Key, e.Value);
                    }
                    pi.Framework.Gui.Chat.Print("Command Performed");
                    return;
                }
                if(args == "func")
                {
                    //pi.Framework.Gui.Chat.Print(UnknownFunction(pi.ClientState.Targets.CurrentTarget.Address).ToString());
                    return;
                }
                if(args == "mem")
                {
                    editingMemory = true;
                    lockedActor = pi.ClientState.Targets.CurrentTarget.Address;
                    memlist = new List<(int, byte)>();
                    foreach (var e in mem)
                    {
                        memlist.Add((e.Key, e.Value));
                    }
                    curPos = 0;// memlist.Count - 1;
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
                if (args == "write0")
                {
                    *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + 0x1980) = 0;
                    return;
                }
                if (args == "write4")
                {
                    *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + 0x1980) = 4;
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

        /*private long UnkFuncDetour(long a1, uint a2, uint a3, long a4, byte a5, byte a6)
        {
            try
            {
                if(a5 != 0)
                pi.Framework.Gui.Chat.Print(Environment.TickCount + "\n" +
                    "a1:" + Convert.ToString(a1, 16) +
                    "\na2:" + a2 +
                    "\na3:" + a3 +
                    "\na4:" + Convert.ToString(a4, 16) +
                    "\na5:" + a5 +
                    "\na6:" + a6);
            }
            catch (Exception e)
            {

            }
            return UnkFuncHook.Original(a1,  a2,  a3,  a4,a5,a6);
        }*/

        [HandleProcessCorruptedStateExceptions]
        private void Tick(Framework framework)
        {
            if (!init) return;
            try
            {
                ActorSet.Clear();
                var s = (UIModule*)GetUIModule;
                foreach (var a in pi.ClientState.Actors) 
                {
                    if (!(a is BattleNpc)) continue;
                    var bnpc = (BattleNpc)a;
                    if (bnpc.CurrentHp == 0) continue;
                    var bnpcKind = bnpc.BattleNpcKind;
                    if (GetIsTargetable(a.Address) == 0) continue;
                    ActorSet.Add((a.Position,
                        a.Name
                        //+ "/" + GetIsTargetable(a.Address)
                        //+ "\nKind: " + oKind
                        //+ "/Subking: " + bnpcKind
                        // + "/0x17D: " + ((BattleChara*)a.Address)->field_0x17D
                        //+ "\nStatus flags: " + bnpc.StatusFlags
                        //+ "\n0x94: " + Convert.ToString(((BattleChara*)a.Address)->Character.GameObject.field_0x94, 2).PadLeft(sizeof(int)*8, '0')
                        //+ "\n0x15B: " + Convert.ToString(((BattleChara*)a.Address)->Character.GameObject.field_0x15B, 2).PadLeft(sizeof(byte)*8, '0')
                        //+ "\nAggroFlags: " + Convert.ToString(((BattleChara*)a.Address)->AggroFlags, 2).PadLeft(sizeof(int) *8, '0')
                        //+ "\nCombatFlags: " + Convert.ToString(((BattleChara*)a.Address)->CombatFlags, 2).PadLeft(sizeof(int)*8, '0')
                        //+ "\nsub_1407437A0: " + Convert.ToString(sub_1407437A0Function(GameObjectManager, a.ActorId), 16)
                        //+ "\nID: " + Convert.ToString(a.ActorId, 16)
                        + "\nH: " + IsHostileMemory(bnpc) + "/" + IsHostileFunction(bnpc)
                        //+ "\n1400C6B90:" + UnknownFunction(a.Address)
                        //+ "\n0x193C:" + *(byte*)(a.Address + 0x193C)
                        //+ "\n" + s->
                        , IsHostileMemory(bnpc) != IsHostileFunction(bnpc) && Environment.TickCount % 1000 > 500)); 
                    if(IsHostileMemory(bnpc) != IsHostileFunction(bnpc))
                    {
                        PluginLog.Information("=== Mismatch of IsHostileMemory and IsHostileFunction ===");
                        PluginLog.Information("PlateType: " + UnknownFunction(a.Address));
                        PluginLog.Information("0x1980: " + *(byte*)(a.Address + 0x1980));
                        PluginLog.Information("0x193C: " + *(byte*)(a.Address + 0x193C));
                        PluginLog.Information("Actor name: " + a.Name);
                        PluginLog.Information("BattleNpcKing: " + bnpc.BattleNpcKind);
                        PluginLog.Information("Status Flags: " + bnpc.StatusFlags);
                        /*var lst = new string[0x2C00];
                        for (int i = 0; i < lst.Length; i++)
                        {
                            lst[i] = Convert.ToString(*(byte*)(a.Address + i), 16).ToUpper();
                        }
                        PluginLog.Information("== Memory dump ==");
                        PluginLog.Information(string.Join("", lst));*/
                        PluginLog.Information("=== Mismatch report end ===");
                        pi.Framework.Gui.Chat.Print("Mismatch between hostile memory and hostile function");
                    }
                }
            }
            catch(Exception e)
            {
                pi.Framework.Gui.Chat.Print("ActorTest error: " + e + "\n" + e.Message);
            }

        }

        bool IsHostileMemory(BattleNpc a)
        {
            return (*(byte*)(a.Address + 0x1980) & (1 << 2)) != 0 && *(byte*)(a.Address + 0x193C) != 1;
        }

        bool IsHostileFunction(BattleNpc a)
        {
            var plateType = UnknownFunction(a.Address);
            //7: yellow, can be attacked, not engaged
            //9: red, engaged with you
            //11: orange, aggroed to you but not attacked yet
            //10: engaged with other actor
            return plateType == 7 || plateType == 9 || plateType == 11 || plateType == 10;
        }

        /*bool GetIsTargetable(GameObject* obj)
        {
            byte bVar1;

            bVar1 = *(byte*)&obj->field_0x94;
            if ((((bVar1 & 2) != 0) && ((bVar1 & 4) != 0)) &&
               ((((uint)obj->RenderFlags >> 0xb & 1) == 0 || (0x7f < bVar1))))
            {
                return (obj->RenderFlags & 0xffffe7f7U) == 0;
            }
            return false;
        }*/

        private void Draw()
        {
            if (manualMemEdit)
            {
                ImGui.Begin("Manual memory editing", ref manualMemEdit);
                ImGui.InputInt("Base", ref membase);
                ImGui.InputText("Address", ref addrStr, 100);
                ImGui.InputText("Value", ref valStr, 100);
                if (ImGui.Button("Read"))
                {
                    try
                    {
                        pi.Framework.Gui.Chat.Print(Convert.ToString(*(byte*)(pi.ClientState.Targets.CurrentTarget.Address + Convert.ToInt32(addrStr, membase)), membase));
                    }
                    catch (Exception e)
                    {
                        pi.Framework.Gui.Chat.Print(e.Message);
                    }
                }
                if (ImGui.Button("Set"))
                {
                    try
                    {
                        *(byte*)(pi.ClientState.Targets.CurrentTarget.Address + Convert.ToInt32(addrStr, membase)) = Convert.ToByte(valStr, membase);
                    }
                    catch(Exception e)
                    {
                        pi.Framework.Gui.Chat.Print(e.Message);
                    }
                }
                ImGui.End();
            }
            if (editingMemory)
            {
                if(ImGui.Begin("Actor memory editor", ref editingMemory))
                {
                    ImGui.Text("Offset: " + memcurAddr);
                    ImGui.Text("Value: " + memcurValue);
                    if (ImGui.Button("Next"))
                    {
                        if(memcurAddr != 0) 
                        {
                            Marshal.WriteByte(lockedActor, memcurAddr, memcurValue);
                        }
                        if(curPos < memlist.Count)
                        {
                            memcurAddr = memlist[curPos].Item1;
                            memcurValue = Marshal.ReadByte(lockedActor, memcurAddr);
                            Marshal.WriteByte(lockedActor, memcurAddr, memlist[curPos].Item2);
                        }
                        curPos++;
                    }
                    ImGui.EndChild();
                }
                ImGui.End();
            }
            var i = 0;
            var somebool = true;
            foreach(var a in ActorSet)
            {
                if (displayOverlay && pi.Framework.Gui.WorldToScreen(new SharpDX.Vector3(a.pos.X, a.pos.Z, a.pos.Y), out var screenPos))
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
                        ImGuiWindowFlags.NoFocusOnAppearing |
                        ImGuiWindowFlags.AlwaysUseWindowPadding);
                    if (a.colored) ImGui.PushStyleColor(ImGuiCol.Text, 0xff0000ff);
                    ImGui.Text(a.text);
                    if (a.colored) ImGui.PopStyleColor();
                    ImGui.End();
                    ImGui.PopStyleVar(2);
                }
            }
        }
    }
}
