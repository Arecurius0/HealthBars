using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;

namespace HealthBars
{
    public class HealthBar
    {
        public HealthBar(Entity entity, HealthBarsSettings settings)
        {
            Entity = entity;
            _DistanceCache = new TimeCache<float>(() => entity.DistancePlayer, 200);
            DebuffPanel = new DebuffPanel(entity);

            Update(entity, settings);
        }
        public bool Skip { get; set; } = false;
        public UnitSettings Settings { get; private set; }
        public RectangleF BackGround { get; set; }
        public DebuffPanel DebuffPanel { get; set; }
        private TimeCache<float> _DistanceCache { get; set; }
        public float Distance => _DistanceCache.Value;
        public Entity Entity { get; }
        public CreatureType Type { get; private set; }
        public Life Life => Entity.GetComponent<Life>();
        public float HpPercent => Life?.HPPercentage ?? 100;
        public float HpWidth { get; set; }
        public float EsWidth { get; set; }

        public Color Color
        {
            get
            {
                if (IsHidden(Entity))
                    return Color.LightGray;

                if (HpPercent <= 0.1f)
                    return Settings.Under10Percent;

                return Settings.Color;
            }
        }

        private bool IsHidden(Entity entity)
        {
            try
            {
                return entity.IsHidden;
            }
            catch
            {
                return false;
            }
        }

        public void Update(Entity entity, HealthBarsSettings settings)
        {
            if (entity.HasComponent<Player>())
            {
                Type = CreatureType.Player;
                Settings = settings.Players;
            }
            else if (entity.HasComponent<Monster>())
            {
                if (entity.IsHostile)
                {
                    switch (entity.GetComponent<ObjectMagicProperties>().Rarity)
                    {
                        case MonsterRarity.White:
                            Type = CreatureType.Normal;
                            Settings = settings.NormalEnemy;
                            break;

                        case MonsterRarity.Magic:
                            Type = CreatureType.Magic;
                            Settings = settings.MagicEnemy;
                            break;

                        case MonsterRarity.Rare:
                            Settings = settings.RareEnemy;
                            Type = CreatureType.Rare;
                            break;

                        case MonsterRarity.Unique:
                            Settings = settings.UniqueEnemy;
                            Type = CreatureType.Unique;
                            break;
                        default:
                            Settings = settings.Minions;
                            Type = CreatureType.Minion;
                            break;
                    }
                }
                else
                {
                    Type = CreatureType.Minion;
                    Settings = settings.Minions;
                }
            }
        }
    }
}
