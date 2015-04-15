using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace AzirTheEmperorOfSoloQueue
{
    class Emperor
    {
        internal static Spell Q, QTrain, W, E, R;
        internal static SpellSlot IgniteSlot;
        internal static Menu Config;
        internal static List<Spell> Spells = new List<Spell>();
        internal static Orbwalking.Orbwalker Orb;
        internal static Vector3 lastSoldierPosition = new Vector3();
        internal static float whenToCast;

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        internal static void Game_OnGameLoad(EventArgs args)
        {
            Config = new Menu("Azir - SoloQ God","azir",true);
            var ts = new Menu("Target Selector", "ts");
            TargetSelector.AddToMenu(ts);
            Config.AddSubMenu(ts);

            var orbz = new Menu("Orbwalker","orb");
            Orb = new Orbwalking.Orbwalker(orbz);
            Config.AddSubMenu(orbz);

            var sel = new Menu("Configuration", "Config");

            var laneClear = new Menu("Lane Clear", "lc");
            laneClear.AddItem(new MenuItem("useQ_LC", "Use Q").SetValue(false));
            laneClear.AddItem(new MenuItem("useW_LC", "Use W").SetValue(false));
            laneClear.AddItem(new MenuItem("useW_TURRET_LC", "Use W on turret's").SetValue(false));
            laneClear.AddItem(new MenuItem("useW_TURRET_LC_X", "Only use if I have > X (W) stacks").SetValue(new Slider(1,2)));
            sel.AddSubMenu(laneClear);

            var insec = new Menu("InSec", "insec");
            insec.AddItem(new MenuItem("insec", "Insec anus").SetValue(new KeyBind('T', KeyBindType.Press)));
            insec.AddItem(new MenuItem("insecWhere", "Insec to").SetValue(new StringList(new[] { "Team", "Nearest turret", "Last position" })));
            sel.AddSubMenu(insec);

            var useE = new Menu("Use E", "useeE");
            useE.AddItem(new MenuItem("eDive", "Turret dive (Toggle)").SetValue(new KeyBind('X',KeyBindType.Toggle)));
            useE.AddItem(new MenuItem("useE", "Use E").SetValue(true));
            foreach (var minion in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsEnemy))
            {
                useE.AddItem(new MenuItem("useE_"+minion.BaseSkinName,"Use E on " + minion.BaseSkinName).SetValue(true));
            }
            sel.AddItem(new MenuItem("useR", "Use R").SetValue(true));
            sel.AddItem(new MenuItem("useAA", "Auto-Attack targets in range").SetValue(true));
            sel.AddItem(new MenuItem("trainMode", "Azir Express").SetValue(new KeyBind('Z', KeyBindType.Press)));
            sel.AddSubMenu(useE);
            Config.AddSubMenu(sel);

            var draw = new Menu("Drawings", "draw");
            draw.AddItem(new MenuItem("drawInsec", "Draw Insec").SetValue(true));
            draw.AddItem(new MenuItem("drawQ", "Draw Q range").SetValue(true));
            draw.AddItem(new MenuItem("drawW", "Draw W range").SetValue(true));
            draw.AddItem(new MenuItem("drawE", "Draw E range").SetValue(true));
            draw.AddItem(new MenuItem("drawSoldier", "Draw Soldier range").SetValue(true));
            Config.AddSubMenu(draw);
            Config.AddToMainMenu();

            Q = new Spell(SpellSlot.Q, 800);
            QTrain = new Spell(SpellSlot.Q, 1600);
            W = new Spell(SpellSlot.W, 450);
            E = new Spell(SpellSlot.E, 2500);
            R = new Spell(SpellSlot.R, 580);
            IgniteSlot = ObjectManager.Player.GetSpellSlot("summonerdot");

            Q.SetSkillshot(0.25f, 0, 500, false, SkillshotType.SkillshotLine);
            QTrain.SetSkillshot(0.25f, 0, 500, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.25f, 100, 500, false, SkillshotType.SkillshotCircle);
            E.SetTargetted(0.2f, 1250);
            R.SetSkillshot(0.5f, 200, 500, false, SkillshotType.SkillshotLine);
            DrawingManager.SpellList.Add(Q);
            DrawingManager.SpellList.Add(W);
            DrawingManager.SpellList.Add(E);
            Game.OnUpdate += Game_OnUpdate;
            GameObject.OnCreate += VectorManager.GameObject_OnCreate;
            GameObject.OnDelete += VectorManager.GameObject_OnDelete;
            DrawingManager.Init();
            oldPos = ObjectManager.Player.ServerPosition;
        }

        internal static void SetWhenToCast()
        {
            var speed = ObjectManager.Player.GetSpell(SpellSlot.Q).SData.MissileSpeed;
            whenToCast = (ObjectManager.Player.Distance(lastSoldierPosition) / speed) - (Game.Ping / 2);
        }
        internal static void Game_OnUpdate(EventArgs args)
        {
//            if (Player.InFountain() || Player.IsRecalling()) return;
            VectorManager.RemoveCorruptedSoldiers();
            EscapeMode();
            FightMode();
            HarassMode();
            LaneClear();
            InSec();
            if (Orbwalking.OrbwalkingMode.Mixed == Orb.ActiveMode || Orbwalking.OrbwalkingMode.LaneClear == Orb.ActiveMode)
            {
                AttackTurret();
            }
            var myPos = ObjectManager.Player.Position;
            foreach (var minion in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsEnemy && h.IsVisible && !h.IsDead).OrderBy(h => h.Health))
            {
                if (Q.IsReady() && ObjectManager.Player.GetSpellDamage(minion, SpellSlot.Q) > minion.Health)
                {
                    Q.Cast(minion, true);
                }
                var nearest = VectorManager.GetSoldierNearPosition(minion.Position);
                if (VectorManager.CanDive(nearest) && ObjectManager.Player.GetSpellDamage(minion, SpellSlot.E) > minion.Health && nearest.Position.Distance(minion.Position) <= 450 && E.IsReady() && (myPos.Y * nearest.Position.X - myPos.X * nearest.Position.Y) - (myPos.Y * minion.Position.X - myPos.X * minion.Position.Y) <= 0)
                {
                    // target within radius
                    E.Cast(nearest.Position, true);
                }
                if (Config.Item("useR").GetValue<bool>() &&
                    ObjectManager.Player.GetSpellDamage(minion, SpellSlot.R) > minion.Health)
                {
//                    R.Cast(minion, true);
                }
            }
        }
        internal static void LaneClear()
        {
            if (Orb.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear) return;
            var miniList = new List<Vector2>();
            foreach (var dumbVector in MinionManager.GetMinions(Q.Range))
            {
                miniList.Add(dumbVector.Position.To2D());
            }
            var pos = MinionManager.GetBestLineFarmLocation(miniList,Q.Width,Q.Range).Position;
            var pos2 = MinionManager.GetBestCircularFarmLocation(miniList, W.Width, W.Range).Position;
            foreach (var minion in MinionManager.GetMinions(Q.Range))
            {
                if (Config.Item("useQ_LC").GetValue<bool>() && Q.IsReady())
                {
                    Q.Cast(pos, true);
                }
                if (Config.Item("useW_LC").GetValue<bool>() && W.IsReady())
                {
                    W.Cast(pos2, true);
                }
            }
        }
        internal static void AttackTurret()
        {
            if (!Config.Item("useW_TURRET_LC").GetValue<bool>()) return;

            if (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Ammo >= Config.Item("useW_TURRET_LC_X").GetValue<Slider>().Value)
            {
                var turret = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsValidTarget(W.Range) && tur.IsValid);
                if (turret != null && W.IsReady())
                {
                    W.Cast(turret.Position);
                }
            }
        }

        internal static void HarassMode()
        {
            if (Orbwalking.OrbwalkingMode.Mixed != Orb.ActiveMode) return;
            var target = TargetSelector.GetTarget(1250, TargetSelector.DamageType.Magical);
            if (target == null) return;
            if (VectorManager.IsWithinSoldierRange(target) && Config.Item("useAA").GetValue<bool>())
            {
                if (Orbwalking.CanAttack())
                {
                    Orbwalking.LastAATick = Environment.TickCount;
                    ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                }
            }
            if (VectorManager.AzirObjects.Count < 1)
            {
                W.Cast(
                    ObjectManager.Player.Distance(target, false) > 450
                        ? VectorManager.MaxSoldierPosition(target.Position)
                        : target.Position, true);
                Orbwalking.ResetAutoAttackTimer();
                ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
            }
            if (VectorManager.IsWithinSoldierRange(target))
            {
                Orbwalking.ResetAutoAttackTimer();
                ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
            }
            if (Q.IsReady())
            {
                if (!VectorManager.IsWithinSoldierRange(target) && VectorManager.AzirObjects.Any(obj => obj.Position.Distance(target.Position,false) >= 400f))
                {
                    Q.Cast(target, true);
                }
            }
        }


        internal static void FightMode()
        {
            if (Orbwalking.OrbwalkingMode.Combo != Orb.ActiveMode) return;
            var target = TargetSelector.GetTarget(1250+450, TargetSelector.DamageType.Magical);
            if (target == null) return;
            if (VectorManager.IsWithinSoldierRange(target) && Config.Item("useAA").GetValue<bool>())
            {
                if (Orbwalking.CanAttack())
                {
                    Orbwalking.LastAATick = Environment.TickCount;
                    ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                }
            }
            if (W.IsReady())
            {
                if (VectorManager.AzirObjects.Count < 2 && target.Distance(VectorManager.MaxSoldierPosition(target.Position),true) <= 450)
                {
                    W.Cast(ObjectManager.Player.Distance(target,false) >= 450 ? VectorManager.MaxSoldierPosition(target.Position) : target.Position, true);
                    Orbwalking.ResetAutoAttackTimer();
                    ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                }
                if (!Q.IsReady() && ObjectManager.Player.Distance(target,false) <= 800f)
                    // we use double because azir soldier double our range.
                {
                    W.Cast(ObjectManager.Player.Distance(target,false) >= 450 ? VectorManager.MaxSoldierPosition(target.Position) : target.Position,true);
                    Orbwalking.ResetAutoAttackTimer();
                    ObjectManager.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                }
            }
            if (QTrain.IsReady())
            {
                QTrain.Cast(target, true);
            }
            var myPos = ObjectManager.Player.Position;
            var nearest = VectorManager.GetSoldierNearPosition(target.Position).Position;
            if (Config.Item("useE").GetValue<bool>() && Config.Item("useE_"+target.BaseSkinName).GetValue<bool>())// && nearest.Distance(target.Position) <= 450 && E.IsReady() && (myPos.Y*nearest.X - myPos.X*nearest.Y) - (myPos.Y*target.Position.X - myPos.X*target.Position.Y) <= 0)
            {
                var projection = target.Position.To2D().ProjectOn(ObjectManager.Player.Position.To2D(), nearest.To2D());
                if (projection.IsOnSegment)
                {
                    // target within radius
                    E.Cast(nearest, true);
                }
            }
        }
        static Vector3 oldPos = new Vector3();
        internal static void InSec()
        {
            if (Config.Item("insec").GetValue<KeyBind>().Active)
            {
                Orbwalking.Orbwalk(null, Game.CursorPos);
                var target = TargetSelector.GetTarget(1200, TargetSelector.DamageType.Magical);

                var pos = target.Position.Extend(ObjectManager.Player.Position, -250);
                if(W.IsReady())
                {
                    if (ObjectManager.Player.GetSpell(SpellSlot.E).State == SpellState.Surpressed || E.IsReady())
                    {
                        var where = VectorManager.MaxSoldierPosition(Game.CursorPos);
                        lastSoldierPosition = where;
                        W.Cast(where);
                    }
                    if (E.IsReady() || ObjectManager.Player.GetSpell(SpellSlot.E).State == SpellState.Surpressed)
                    {
                        SetWhenToCast();
                        E.Cast(lastSoldierPosition, true);
                    }
                    if (QTrain.IsReady() && Environment.TickCount - whenToCast > 0)
                    {
                        Q.Cast(ObjectManager.Player.Position + Vector3.Normalize(pos - ObjectManager.Player.Position) * Q.Range, true);
                    }
                }
                if (R.IsReady() && ObjectManager.Player.Distance(target,false) < 250)
                {
                    var whereToInsec = Config.Item("insecWhere").GetValue<StringList>().SelectedIndex;
                    // 1 = Team, 2 = Turret, 3 = Last pos
                    switch(whereToInsec)
                    {
                        case 1:
                            var end = ObjectManager.Player.Position.Extend(ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(h => h.IsAlly && h.IsVisible && h.Distance(ObjectManager.Player,false) < 1200).Position,R.Range);
                            var obj = Geometry.VectorMovementCollision(ObjectManager.Player.Position.To2D(), end.To2D(), R.Speed, target.Position.To2D(), target.MoveSpeed, R.Delay);
                            foreach (var wtf in obj)
                            {
                                if (wtf != "NaN")
                                    R.Cast(end, true);
                            }
                            break;
                        case 2:
                            var turret = ObjectManager.Player.Position.Extend(ObjectManager.Get<Obj_AI_Turret>().OrderBy(h => h.Distance(ObjectManager.Player,false)).FirstOrDefault(t => t.IsValid).Position,R.Range);
                            var TurretObj = Geometry.VectorMovementCollision(ObjectManager.Player.Position.To2D(), turret.To2D(), R.Speed, target.Position.To2D(), target.MoveSpeed, R.Delay);
                            foreach (var wtf in TurretObj)
                            {
                                if (wtf != "NaN")
                                    R.Cast(turret, true);
                            }
                            break;
                        case 3:
                        default:
                            var lastpos = ObjectManager.Player.Position.Extend(oldPos,R.Range);
                            var lastPosObj = Geometry.VectorMovementCollision(ObjectManager.Player.Position.To2D(), lastpos.To2D(), R.Speed, target.Position.To2D(), target.MoveSpeed, R.Delay);
                            foreach (var wtf in lastPosObj)
                            {
                                Game.PrintChat(""+wtf);
                                if (wtf != "NaN")
                                    R.Cast(lastpos, true);
                            }
                            break;
                    }
                }
            }
        }
        internal static void EscapeMode()
        {
            if (!Config.Item("trainMode").GetValue<KeyBind>().Active) return;
            Orbwalking.Orbwalk(null, Game.CursorPos);
            if (W.IsReady())
            {
                if (ObjectManager.Player.GetSpell(SpellSlot.E).State == SpellState.Surpressed || E.IsReady())
                {
                    var where = VectorManager.MaxSoldierPosition(Game.CursorPos);
                    lastSoldierPosition = where;
                    W.Cast(where);
                }
                if (E.IsReady() || ObjectManager.Player.GetSpell(SpellSlot.E).State == SpellState.Surpressed)
                {
                    SetWhenToCast();
                    E.Cast(lastSoldierPosition, true);
                }
                if (QTrain.IsReady() && Environment.TickCount - whenToCast > 0)
                {
                    Q.Cast(ObjectManager.Player.Position + Vector3.Normalize(Game.CursorPos - ObjectManager.Player.Position) * Q.Range, true);
                }
            }
        }
    }
}
