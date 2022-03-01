﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;

namespace HealthBars
{
    public class HealthBars : BaseSettingsPlugin<HealthBarsSettings>
    {
        private Camera camera;
        private bool CanTick = true;
        private readonly List<Element> ElementForSkip = new List<Element>();
        private string IGNORE_FILE { get; } = Path.Combine("config", "ignored_entities.txt");
        private List<string> IgnoredEntities { get; set; }

        private IngameUIElements ingameUI;
        private CachedValue<bool> ingameUICheckVisible;
        private Vector2 oldplayerCord;
        private Entity Player;
        private HealthBar PlayerBar;
        private RectangleF windowRectangle;
        private Size2F windowSize;

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
            Graphics.InitImage("healthbar.png");
        }

        public override bool Initialise()
        {
            Player = GameController.Player;
            ingameUI = GameController.IngameState.IngameUi;
            PlayerBar = new HealthBar(Player, Settings);

            GameController.EntityListWrapper.PlayerUpdate += (sender, args) =>
            {
                Player = GameController.Player;

                PlayerBar = new HealthBar(Player, Settings);
            };

            ingameUICheckVisible = new TimeCache<bool>(() =>
            {
                windowRectangle = GameController.Window.GetWindowRectangleReal();
                windowSize = new Size2F(windowRectangle.Width / 2560, windowRectangle.Height / 1600);
                camera = GameController.Game.IngameState.Camera;
                
                return ingameUI.BetrayalWindow.IsVisibleLocal || ingameUI.SellWindow.IsVisibleLocal ||
                       ingameUI.DelveWindow.IsVisibleLocal || ingameUI.IncursionWindow.IsVisibleLocal ||
                       ingameUI.UnveilWindow.IsVisibleLocal || ingameUI.TreePanel.IsVisibleLocal || ingameUI.Atlas.IsVisibleLocal ||
                       ingameUI.CraftBench.IsVisibleLocal || ingameUI.UltimatumProgressWindow.IsVisibleLocal;
            }, 250);
            ReadIgnoreFile();

            return true;
        }

        private void ReadIgnoreFile()
        {
            var path = Path.Combine(DirectoryFullName, IGNORE_FILE);
            if (File.Exists(path))
            {
                IgnoredEntities = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")).ToList();
            } 
            else
            {
                LogError($"Ignored entities file does not exist. Path: {path}");
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            ingameUI = GameController.IngameState.IngameUi;
            ReadIgnoreFile();
        }

        private bool SkipHealthBar(HealthBar healthBar)
        {
            if (healthBar == null) return true;
            if (healthBar.Settings == null) return true;
            if (!healthBar.Settings.Enable) return true;
            if (!healthBar.Entity.IsAlive) return true;
            if (healthBar.HpPercent < 0.001f) return true;
            if (healthBar.Type == CreatureType.Minion && healthBar.HpPercent * 100 > Settings.ShowMinionOnlyBelowHp) return true;
/*            if (healthBar.Entity.League == LeagueType.Legion && healthBar.Entity.IsHidden 
                && healthBar.Entity.Rarity != MonsterRarity.Unique 
                && healthBar.Entity.Rarity != MonsterRarity.Rare) return true;*/

            return false;
        }

        public void HpBarWork(HealthBar healthBar)
        {
            if (healthBar == null) return;
            healthBar.Skip = SkipHealthBar(healthBar);
            if (healthBar.Skip) return;

            var healthBarDistance = healthBar.Distance;
            if (healthBarDistance > Settings.LimitDrawDistance)
            {
                healthBar.Skip = true;
                return;
            }

            var worldCoords = healthBar.Entity.Pos;
            worldCoords.Z += Settings.GlobalZ;
            var mobScreenCoords = camera.WorldToScreen(worldCoords);
            if (mobScreenCoords == Vector2.Zero) return;
            var scaledWidth = healthBar.Settings.Width * windowSize.Width;
            var scaledHeight = healthBar.Settings.Height * windowSize.Height;

            healthBar.BackGround = new RectangleF(mobScreenCoords.X - scaledWidth / 2f, mobScreenCoords.Y - scaledHeight / 2f, scaledWidth,
                scaledHeight);

            if (healthBarDistance > 80 && !windowRectangle.Intersects(healthBar.BackGround))
            {
                healthBar.Skip = true;
                return;
            }

            foreach (var forSkipBar in ElementForSkip)
            {
                if (forSkipBar.IsVisibleLocal && forSkipBar.GetClientRectCache.Intersects(healthBar.BackGround))
                {
                    healthBar.Skip = true;
                }
            }

            healthBar.HpWidth = healthBar.HpPercent * scaledWidth;
            healthBar.EsWidth = healthBar.Life.ESPercentage * scaledWidth;
        }

        public override Job Tick()
        {
            if (Settings.MultiThreading && GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster].Count >=
                Settings.MultiThreadingCountEntities)
            {
                return new Job(nameof(HealthBars), TickLogic);

                // return GameController.MultiThreadManager.AddJob(TickLogic, nameof(HealthBars));
            }

            TickLogic();
            return null;
        }

        private void TickLogic()
        {
            CanTick = true;

            if (ingameUICheckVisible.Value
                || camera == null
                || GameController.Area.CurrentArea.IsTown && !Settings.ShowInTown)
            {
                CanTick = false;
                return;
            }

            var monster = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster];
            foreach (var validEntity in monster)
            {
                var healthBar = validEntity.GetHudComponent<HealthBar>();
                try
                {
                    HpBarWork(healthBar);
                }
                catch (Exception e)
                {
                    DebugWindow.LogError(e.Message);
                }
            }

            foreach (var validEntity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player])
            {
                var healthBar = validEntity.GetHudComponent<HealthBar>();

                if (healthBar != null)
                    HpBarWork(healthBar);
            }
        }

        public override void Render()
        {
            if (!CanTick) return;

            foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
            {
                var healthBar = entity.GetHudComponent<HealthBar>();
                if (healthBar == null) continue;

                if (healthBar.Skip)
                {
                    healthBar.Skip = false;
                    continue;
                }

                DrawBar(healthBar);
            }

            foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player])
            {
                var healthBar = entity.GetHudComponent<HealthBar>();
                if (healthBar == null) continue;

                if (healthBar.Skip)
                {
                    healthBar.Skip = false;
                    continue;
                }

                DrawBar(healthBar);
            }

            if (Settings.SelfHealthBarShow)
            {
                var worldCoords = PlayerBar.Entity.Pos;
                worldCoords.Z += Settings.PlayerZ;
                var result = camera.WorldToScreen(worldCoords);

                if (Math.Abs(oldplayerCord.X - result.X) < 40 || Math.Abs(oldplayerCord.X - result.Y) < 40)
                    result = oldplayerCord;
                else
                    oldplayerCord = result;

                var scaledWidth = PlayerBar.Settings.Width * windowSize.Width;
                var scaledHeight = PlayerBar.Settings.Height * windowSize.Height;

                PlayerBar.BackGround = new RectangleF(result.X - scaledWidth / 2f, result.Y - scaledHeight / 2f, scaledWidth,
                    scaledHeight);

                PlayerBar.HpWidth = PlayerBar.HpPercent * scaledWidth;
                PlayerBar.EsWidth = PlayerBar.Life.ESPercentage * scaledWidth;
                DrawBar(PlayerBar);
            }
        }

        public void DrawBar(HealthBar bar)
        {
            if (Settings.ImGuiRender)
            {
                Graphics.DrawBox(bar.BackGround, bar.Settings.BackGround);
                Graphics.DrawBox(new RectangleF(bar.BackGround.X, bar.BackGround.Y, bar.HpWidth, bar.BackGround.Height), bar.Color);
            }
            else
            {
                Graphics.DrawImage("healthbar.png", bar.BackGround, bar.Settings.BackGround);

                Graphics.DrawImage("healthbar.png", new RectangleF(bar.BackGround.X, bar.BackGround.Y, bar.HpWidth, bar.BackGround.Height),
                    bar.Color);
            }

            Graphics.DrawBox(new RectangleF(bar.BackGround.X, bar.BackGround.Y, bar.EsWidth, bar.BackGround.Height * 0.33f), Color.Aqua);
            bar.BackGround.Inflate(1, 1);
            Graphics.DrawFrame(bar.BackGround, bar.Settings.Outline, 1);

            ShowPercents(bar);
            ShowNumbersInHealthbar(bar);
            ShowDebuffPanel(bar);
        }

        private void ShowNumbersInHealthbar(HealthBar bar)
        {
            if (!bar.Settings.ShowHealthText && !bar.Settings.ShowEnergyShieldText) return;

            string healthBarText = "";
            if (bar.Settings.ShowHealthText)
            {
                healthBarText = $"{bar.Life.CurHP.ToString("N0")}/{bar.Life.MaxHP.ToString("N0")}";
            } 
            else if (bar.Settings.ShowEnergyShieldText)
            {
                healthBarText = $"{bar.Life.CurES.ToString("N0")}/{bar.Life.MaxES.ToString("N0")}";
            }

            Graphics.DrawText(healthBarText,
                new Vector2(bar.BackGround.Center.X, bar.BackGround.Center.Y - Graphics.Font.Size / 2f),
                bar.Settings.HealthTextColor, 
                FontAlign.Center);
        }

        private void ShowPercents(HealthBar bar)
        {
            if (!bar.Settings.ShowHealthPercents && !bar.Settings.ShowEnergyShieldPercents) return;

            float percents = 0;
            if (bar.Settings.ShowHealthPercents)
            {
                percents = bar.Life.HPPercentage;
            }
            else if (bar.Settings.ShowEnergyShieldPercents)
            {
                percents = bar.Life.ESPercentage;
            }

            Graphics.DrawText(FloatToPercentString(percents),
                new Vector2(bar.BackGround.Right, bar.BackGround.Center.Y - Graphics.Font.Size / 2f),
                bar.Settings.PercentTextColor);
        }

        private void ShowDebuffPanel(HealthBar bar)
        {
            if (!bar.Settings.ShowDebuffPanel) return;

            Graphics.DrawText(bar.DebuffPanel.Bleed.Count.ToString(),
                new Vector2(bar.BackGround.Left, bar.BackGround.Top - Graphics.Font.Size),
                bar.DebuffPanel.Bleed.Count == 8 ? Color.Green : Color.Red);

            Graphics.DrawText(bar.DebuffPanel.CorruptedBlood.Count.ToString(),
                new Vector2(bar.BackGround.Left + 20, bar.BackGround.Top - Graphics.Font.Size),
                bar.DebuffPanel.CorruptedBlood.Count == 10 ? Color.Green : Color.Red);

            if (bar.DebuffPanel.CurseVulnerability != null)
            {
                Graphics.DrawText($"{Convert.ToInt32(bar.DebuffPanel.CurseVulnerability.Timer).ToString()}",
                    new Vector2(bar.BackGround.Left + 40, bar.BackGround.Top - Graphics.Font.Size),
                    bar.DebuffPanel.CurseVulnerability.Timer > 2 ? Color.Green : Color.Red);
            }

            if (bar.DebuffPanel.AuraPride != null)
            {
                Graphics.DrawText("P",
                    new Vector2(bar.BackGround.Left + 60, bar.BackGround.Top - Graphics.Font.Size),
                    Color.Green);
            }
        }

        private string FloatToPercentString (float number)
        {
            return $"{Math.Floor(number * 100).ToString(CultureInfo.InvariantCulture)}";
        }

        public override void EntityAdded(Entity Entity)
        {
            if (Entity.Type != EntityType.Monster && Entity.Type != EntityType.Player 
                || Entity.Address == GameController.Player.Address 
                || Entity.Type == EntityType.Daemon) return;

            if (Entity.GetComponent<Life>() != null && !Entity.IsAlive) return;
            if (IgnoredEntities.Any(x => Entity.Path.StartsWith(x))) return;
            Entity.SetHudComponent(new HealthBar(Entity, Settings));
        }
    }
}
