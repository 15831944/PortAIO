using EloBuddy; 
using LeagueSharp.Common; 
 namespace vEvade.Helpers
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using LeagueSharp;
    using LeagueSharp.Common;

    using SharpDX;

    using vEvade.Managers;
    using vEvade.Spells;

    #endregion

    internal static class Collisions
    {
        #region Static Fields

        private static bool haveYasuo;

        private static Vector2 yasuoWallPos;

        private static int yasuoWallTick;

        #endregion

        #region Public Methods and Operators

        public static Vector2 GetCollision(SpellInstance spell)
        {
            var result = new List<DetectedCollision>();
            var from = spell.GetMissilePosition(0);
            spell.ForceDisabled = false;

            foreach (var type in spell.Data.CollisionObjects)
            {
                switch (type)
                {
                    case CollisionableObjects.Minions:
                        result.AddRange(
                            from minion in
                                ObjectManager.Get<Obj_AI_Minion>()
                                .Where(
                                    i =>
                                    i.IsValidTarget(1200, false, @from.To3D())
                                    && (i.IsJungle() || (i.IsAlly && (i.IsMinion() || i.IsPet()))))
                            let pred =
                                FastPrediction(
                                    @from,
                                    minion,
                                    Math.Max(0, spell.Data.Delay - (Utils.GameTimeTickCount - spell.StartTick)),
                                    spell.Data.MissileSpeed)
                            let pos = pred.Position
                            where
                                spell.Data.RawRadius + (!pred.IsMoving ? minion.BoundingRadius - 15 : 0)
                                - pos.Distance(@from, spell.End, true) > 0
                            select
                                new DetectedCollision
                                    {
                                        Position = pos.ProjectOn(spell.End, spell.Start).LinePoint + spell.Direction * 30,
                                        Distance = pos.Distance(@from)
                                    });
                        break;
                    case CollisionableObjects.Heroes:
                        result.AddRange(
                            from hero in HeroManager.Allies.Where(i => !i.IsMe && i.IsValidTarget(1200, false))
                            let pos =
                                FastPrediction(
                                    @from,
                                    hero,
                                    Math.Max(0, spell.Data.Delay - (Utils.GameTimeTickCount - spell.StartTick)),
                                    spell.Data.MissileSpeed).Position
                            where spell.Data.RawRadius + 30 - pos.Distance(@from, spell.End, true) > 0
                            select
                                new DetectedCollision
                                    {
                                        Position = pos.ProjectOn(spell.End, spell.Start).LinePoint + spell.Direction * 30,
                                        Distance = pos.Distance(@from)
                                    });
                        break;
                    case CollisionableObjects.YasuoWall:
                        if (!haveYasuo || spell.Data.MissileSpeed == 0)
                        {
                            continue;
                        }

                        var wall =
                            ObjectManager.Get<Obj_GeneralParticleEmitter>()
                                .FirstOrDefault(
                                    i => i.IsValid && new Regex("Yasuo_.+_W_windwall.\\.troy").IsMatch(i.Name));

                        if (wall == null)
                        {
                            continue;
                        }

                        var wallWidth = 250 + 50 * Convert.ToInt32(wall.Name.Substring(wall.Name.Length - 6, 1));
                        var wallDirection = (wall.Position.To2D() - yasuoWallPos).Normalized().Perpendicular();
                        var wallStart = wall.Position.To2D() + wallWidth / 2f * wallDirection;
                        var wallEnd = wallStart - wallWidth * wallDirection;
                        var wallPolygon = new Polygons.Line(wallStart, wallEnd, 75).ToPolygon();
                        var intersects = new List<Vector2>();

                        for (var i = 0; i < wallPolygon.Points.Count; i++)
                        {
                            var inter =
                                wallPolygon.Points[i].Intersection(
                                    wallPolygon.Points[i != wallPolygon.Points.Count - 1 ? i + 1 : 0],
                                    from,
                                    spell.End);

                            if (inter.Intersects)
                            {
                                intersects.Add(inter.Point);
                            }
                        }

                        if (intersects.Count > 0)
                        {
                            var intersect = intersects.OrderBy(i => i.Distance(from)).First();
                            var time = Utils.GameTimeTickCount
                                       + Math.Max(0, spell.Data.Delay - (Utils.GameTimeTickCount - spell.StartTick))
                                       + 100 + intersect.Distance(from) / spell.Data.MissileSpeed * 1000;

                            if (time - yasuoWallTick < 4000)
                            {
                                if (spell.Type != SpellType.MissileLine)
                                {
                                    spell.ForceDisabled = true;
                                }

                                return intersect;
                            }
                        }
                        break;
                }
            }

            return result.Count > 0 ? result.OrderBy(i => i.Distance).First().Position : Vector2.Zero;
        }

        public static void Init()
        {
            if (HeroManager.Allies.All(i => i.ChampionName != "Yasuo"))
            {
                return;
            }

            haveYasuo = true;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
        }

        #endregion

        #region Methods

        private static Prediction FastPrediction(Vector2 from, Obj_AI_Base unit, int delay, int speed)
        {
            var d = (delay / 1000f + (speed == 0 ? 0 : from.Distance(unit) / speed)) * unit.MoveSpeed;
            var path = unit.GetWaypoints();

            return path.PathLength() > d
                       ? new Prediction { IsMoving = true, Position = path.CutPath((int)d)[0] }
                       : new Prediction { IsMoving = false, Position = path[path.Count - 1] };
        }

        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsValid || !sender.IsAlly || args.SData.Name != "YasuoWMovingWall")
            {
                return;
            }

            yasuoWallTick = Utils.GameTimeTickCount;
            yasuoWallPos = sender.ServerPosition.To2D();
        }

        #endregion

        private class DetectedCollision
        {
            #region Fields

            public float Distance;

            public Vector2 Position;

            #endregion
        }

        private class Prediction
        {
            #region Fields

            public bool IsMoving;

            public Vector2 Position;

            #endregion
        }
    }
}