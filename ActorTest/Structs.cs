using Dalamud.Game.ClientState.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ActorTest
{
    class Structs
    {
		[StructLayout(LayoutKind.Explicit, Size = 0x1A0)]
		public unsafe struct GameObject
		{
			[FieldOffset(ActorOffsets.Name)] public fixed byte Name[30];
			[FieldOffset(ActorOffsets.ActorId)] public uint ObjectID;
			[FieldOffset(ActorOffsets.OwnerId)] public uint OwnerID;
			[FieldOffset(ActorOffsets.ObjectKind)] public byte ObjectKind;
			[FieldOffset(ActorOffsets.SubKind)] public byte SubKind;
			[FieldOffset(0xF0)] public void* DrawObject;
			[FieldOffset(0x104)] public int RenderFlags;
			[FieldOffset(0x94)] public int field_0x94;
			[FieldOffset(0x15B)] public byte field_0x15B;
		}

		[StructLayout(LayoutKind.Explicit, Size = 0x19C8)]
		public unsafe struct Character
		{
			[FieldOffset(0x0)] public GameObject GameObject;
			[FieldOffset(ActorOffsets.PlayerCharacterTargetActorId)] public uint PlayerCharacterTargetActorId;
			[FieldOffset(ActorOffsets.BattleNpcTargetActorId)] public uint BattleNpcTargetActorId;
			[FieldOffset(ActorOffsets.CompanyTag)] public fixed byte CompanyTag[7];
			[FieldOffset(ActorOffsets.NameId)] public int NameID;
			[FieldOffset(0x1950)] public uint CompanionOwnerID;
			[FieldOffset(ActorOffsets.CurrentWorld)] public ushort CurrentWorld;
			[FieldOffset(ActorOffsets.HomeWorld)] public ushort HomeWorld;
			[FieldOffset(ActorOffsets.CurrentHp)] public int CurrentHp;
			[FieldOffset(0x19A0)] public byte StatusFlags;
		}

		[StructLayout(LayoutKind.Explicit, Size = 0x2BE0)]
		public unsafe struct BattleChara
		{
			[FieldOffset(0x0)] public Character Character;
			[FieldOffset(0x18F1)] public int field_0x18F1;
			[FieldOffset(0x1906)] public int field_0x1906;
			[FieldOffset(6543)] public int AggroFlags;
			[FieldOffset(6560)] public int CombatFlags;
			[FieldOffset(0x17D)] public int field_0x17D;

		}
	}
}
