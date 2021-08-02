using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;

namespace HealthBars
{
    public class DebuffPanel
    {
        public DebuffPanel(Entity entity)
        {
            _Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        private Entity _Entity { get; }

        public List<Buff> Bleed => _Entity.Buffs.Where(b => b.Name == "bleeding_stack").ToList();
        public List<Buff> CorruptedBlood => _Entity.Buffs.Where(b => b.Name == "corrupted_blood").ToList();
        public Buff CurseVulnerability => _Entity.Buffs.FirstOrDefault(b => b.Name == "curse_vulnerability");
        public Buff AuraPride => _Entity.Buffs.FirstOrDefault(b => b.Name == "player_physical_damage_aura");
    }
}
