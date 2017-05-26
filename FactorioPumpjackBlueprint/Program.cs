﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FactorioPumpjackBlueprint
{
    class Program
    {
        static Blueprint LayPipes(Blueprint bp, bool useSpeed3, bool placeBeacons)
        {
            // Yes, this is a lazy copy
            bp = Blueprint.ImportBlueprintString(bp.ExportBlueprintString());

            double minx = bp.Entities.Select(e => e.Position.X).Min();
            double miny = bp.Entities.Select(e => e.Position.Y).Min();

            foreach (var entity in bp.Entities)
            {
                entity.Position.Sub(minx - 5, miny - 5);
            }

            double maxx = bp.Entities.Select(e => e.Position.X).Max();
            double maxy = bp.Entities.Select(e => e.Position.Y).Max();

            int width = (int)Math.Ceiling(maxx) + 6;
            int height = (int)Math.Ceiling(maxy) + 6;

            Entity[,] occupant = new Entity[width, height];

            foreach (var entity in bp.Entities)
            {
                if (!entity.Name.Equals("pumpjack"))
                    continue;
                int x = (int)entity.Position.X;
                int y = (int)entity.Position.Y;
                occupant[x - 1, y - 1] = entity;
                occupant[x - 1, y] = entity;
                occupant[x - 1, y + 1] = entity;
                occupant[x, y - 1] = entity;
                occupant[x, y] = entity;
                occupant[x, y + 1] = entity;
                occupant[x + 1, y - 1] = entity;
                occupant[x + 1, y] = entity;
                occupant[x + 1, y + 1] = entity;
            }

            List<Entity> toAdd = new List<Entity>();
            foreach (var entity in bp.Entities)
            {
                if (!entity.Name.Equals("pumpjack"))
                    continue;
                Position p = null;
                int tries = 0;
                do
                {
                    p = new Position(entity.Position);
                    switch (entity.Direction)
                    {
                        case Direction.North: p.Add(1, -2); break;
                        case Direction.East: p.Add(2, -1); break;
                        case Direction.South: p.Add(-1, 2); break;
                        case Direction.West: p.Add(-2, 1); break;
                        default: p = null; break;
                    }
                    if (p == null || occupant[(int)p.X, (int)p.Y] == null)
                    {
                        break;
                    }
                    p = null;
                    entity.Direction = (entity.Direction + 2) % 8;
                } while (++tries < 4);
                if (p == null)
                    continue;
                toAdd.Add(new Entity("pipe", p));
            }
            foreach (var e in toAdd)
            {
                bp.Entities.Add(e);
            }
            toAdd.Clear();

            IList<Entity> pipes = bp.Entities.Where(e => string.Equals(e.Name, "pipe")).ToList();
            IDictionary<int, int[,]> distanceMap = new Dictionary<int, int[,]>();
            var offsets = new Coord[] {
                new Coord(-1,0),
                new Coord(0,-1),
                new Coord(1,0),
                new Coord(0,1)
            };
            foreach (Entity pipe in pipes)
            {
                int[,] distanceField = new int[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        distanceField[x, y] = -1;
                    }
                }
                Queue<Coord> openQueue = new Queue<Coord>();
                openQueue.Enqueue(new Coord(pipe.Position));
                while (openQueue.Count > 0)
                {
                    Coord c = openQueue.Dequeue();
                    if (c.X < 0 || c.Y < 0 || c.X >= width || c.Y >= height || occupant[c.X, c.Y] != null || distanceField[c.X, c.Y] != -1)
                        continue;
                    int smallest = int.MaxValue;
                    foreach (var offset in offsets)
                    {
                        Coord t = c.Add(offset);
                        if (t.X < 0 || t.Y < 0 || t.X >= width || t.Y >= height)
                            continue;
                        int d = distanceField[t.X, t.Y];
                        if (d != -1)
                        {
                            smallest = Math.Min(smallest, d);
                        }
                        else
                        {
                            openQueue.Enqueue(t);
                        }
                    }
                    distanceField[c.X, c.Y] = (smallest == int.MaxValue) ? 0 : smallest + 1;
                }
                distanceMap.Add(pipe.EntityNumber, distanceField);
            }

            var newPipeSet = new HashSet<Coord>();
            var allPipeIds = pipes.Select(p => p.EntityNumber).ToList();
            var allEdges = new List<Edge>();
            var mstEdges = new HashSet<Edge>();
            var mstIds = new HashSet<int>();
            for (int i = 0; i < pipes.Count; i++)
            {
                var p = pipes[i];
                var d = distanceMap[p.EntityNumber];
                for (int j = 0; j < pipes.Count; j++)
                {
                    if (i == j)
                        continue;
                    var c = new Coord(pipes[j].Position);
                    var distance = d[c.X, c.Y];
                    if (distance == -1)
                        continue;
                    allEdges.Add(new Edge() { Start = p.EntityNumber, End = pipes[j].EntityNumber, Distance = distance });
                }
            }
            allEdges = allEdges.OrderBy(e => e.Distance).ToList();
            Edge edge = allEdges.First();
            mstIds.Add(edge.Start);
            mstIds.Add(edge.End);
            mstEdges.Add(edge);
            while (mstIds.Count < allPipeIds.Count)
            {
                edge = allEdges.First(e => (!mstIds.Contains(e.Start) && mstIds.Contains(e.End)) || (mstIds.Contains(e.Start) && !mstIds.Contains(e.End)));
                mstIds.Add(edge.Start);
                mstIds.Add(edge.End);
                mstEdges.Add(edge);
            }
            var idToPipeMap = pipes.ToDictionary(p => p.EntityNumber);
            foreach (var mstEdge in mstEdges)
            {
                Entity pipe1 = idToPipeMap[mstEdge.Start];
                Entity pipe2 = idToPipeMap[mstEdge.End];
                Coord start = new Coord(pipe1.Position);
                Coord end = new Coord(pipe2.Position);
                var distanceField = distanceMap[pipe1.EntityNumber];
                if (distanceField[end.X, end.Y] == -1)
                    continue;
                Coord c = end;
                while (true)
                {
                    int smallest = int.MaxValue;
                    Coord next = null;
                    foreach (var offset in offsets)
                    {
                        Coord t = c.Add(offset);
                        if (t.X < 0 || t.Y < 0 || t.X >= width || t.Y >= height)
                            continue;
                        int d = distanceField[t.X, t.Y];
                        if (d != -1 && d < smallest)
                        {
                            smallest = d;
                            next = t;
                        }
                    }
                    if (next == null || next.Equals(start))
                    {
                        break;
                    }
                    c = next;
                    newPipeSet.Add(c);
                }
            }

            foreach (var pipe in pipes)
            {
                newPipeSet.Remove(new Coord(pipe.Position));
            }

            var allPipes = new HashSet<Coord>();
            allPipes.UnionWith(pipes.Select(e => new Coord(e.Position)));
            allPipes.UnionWith(newPipeSet);
            
            var ugPipes = ReplaceStraightPipeWithUnderground(newPipeSet, 1, allPipes);

            foreach (var ugPipe in ugPipes)
            {
                bp.Entities.Add(ugPipe);
            }

            foreach (var p in newPipeSet)
            {
                bp.Entities.Add(new Entity("pipe", p.X, p.Y));
            }

            if(useSpeed3)
            {
                foreach(var pumpjack in bp.Entities.Where(e => e.Name.Equals("pumpjack")))
                {
                    pumpjack.Items = new List<Item>() { new Item() { Name = "speed-module-3", Count = 2 } };
                }
            }

            double oilFlow = bp.Entities.Select(e => e.Name.Equals("pumpjack") ? (useSpeed3 ? 2 : 1) : 0).Sum();
            int pipeCount = bp.Entities.Count(e => e.Name.Contains("pipe"));
            bp.extraData = new { PipeCount = pipeCount, Fitness = -pipeCount, OilProduction = oilFlow };



            bp.NormalizePositions();

            return bp;
        }

        static HashSet<Entity> ReplaceStraightPipeWithUnderground(HashSet<Coord> pipesToReplace, int minGapToReplace = 1, HashSet<Coord> allPipes = null)
        {
            const int MAX_UNDERGROUND_PIPE_DISTANCE = 11;

            if (allPipes == null)
            {
                allPipes = pipesToReplace;
            }

            var undergroundPipes = new HashSet<Entity>();
            var ugPipesEndPointsY = new HashSet<Coord>();
            var ugPipesEndPointsX = new HashSet<Coord>();

            int minx = allPipes.Select(c => c.X).Min();
            int miny = allPipes.Select(c => c.Y).Min();
            int maxx = allPipes.Select(c => c.X).Max();
            int maxy = allPipes.Select(c => c.Y).Max();

            for (int x = minx; x <= maxx; x++)
            {
                for (int y = miny; y <= maxy; y++)
                {
                    int yend = y;
                    while (yend - y < MAX_UNDERGROUND_PIPE_DISTANCE &&
                        pipesToReplace.Contains(new Coord(x, yend)) &&
                        !allPipes.Contains(new Coord(x - 1, yend)) &&
                        !allPipes.Contains(new Coord(x + 1, yend)))
                    {
                        yend++;
                    }
                    yend--;
                    if(yend - y > minGapToReplace)
                    {
                        undergroundPipes.Add(new Entity("pipe-to-ground", x, y, Direction.North));
                        undergroundPipes.Add(new Entity("pipe-to-ground", x, yend, Direction.South));
                        ugPipesEndPointsY.Add(new Coord(x, yend));
                        y = yend;
                    }
                }
            }

            for (int y = miny; y <= maxy; y++)
            {
                for (int x = minx; x <= maxx; x++)
                {
                    int xend = x;
                    while (xend - x < MAX_UNDERGROUND_PIPE_DISTANCE &&
                        pipesToReplace.Contains(new Coord(xend, y)) &&
                        !allPipes.Contains(new Coord(xend, y - 1)) &&
                        !allPipes.Contains(new Coord(xend, y + 1)))
                    {
                        xend++;
                    }
                    xend--;
                    if (xend - x > minGapToReplace)
                    {
                        undergroundPipes.Add(new Entity("pipe-to-ground", x, y, Direction.West));
                        undergroundPipes.Add(new Entity("pipe-to-ground", xend, y, Direction.East));
                        ugPipesEndPointsX.Add(new Coord(xend, y));
                        x = xend;
                    }
                }
            }

            foreach(Entity ugPipe in undergroundPipes)
            {
                if(ugPipe.Direction == Direction.North)
                {
                    int x = (int)ugPipe.Position.X;
                    int y = (int)ugPipe.Position.Y;
                    for(; y <= maxy && !ugPipesEndPointsY.Contains(new Coord(x, y)); y++)
                    {
                        pipesToReplace.Remove(new Coord(x, y));
                    }
                    pipesToReplace.Remove(new Coord(x, y));
                }
                else if(ugPipe.Direction == Direction.West)
                {
                    int x = (int)ugPipe.Position.X;
                    int y = (int)ugPipe.Position.Y;
                    for (; x <= maxx && !ugPipesEndPointsX.Contains(new Coord(x, y)); x++)
                    {
                        pipesToReplace.Remove(new Coord(x, y));
                    }
                    pipesToReplace.Remove(new Coord(x, y));
                }
            }

            return undergroundPipes;
        }

        [STAThreadAttribute]
        static void Main(string[] args)
        {
            Blueprint originalBp = Blueprint.ImportBlueprintString(Clipboard.GetText());

            if (originalBp == null)
            {
                Console.WriteLine("Could not load blueprint");
                return;
            }

            bool useSpeed3 = true;
            bool useBeacons = false;

            int iterationsWithoutImprovement = 0;
            Blueprint bestBp = Blueprint.ImportBlueprintString(originalBp.ExportBlueprintString());
            Blueprint bestFinishedBp = LayPipes(originalBp, useSpeed3, useBeacons);
            double bestFitness = bestFinishedBp.extraData.Fitness;
            Console.WriteLine("Found layout with " + bestFinishedBp.extraData.PipeCount + " pipes after " + iterationsWithoutImprovement + " iterations.");
            Random rng = new Random();

            while(++iterationsWithoutImprovement <= 250)
            {
                Blueprint bp = Blueprint.ImportBlueprintString(bestBp.ExportBlueprintString());
                var pumpjackIdMap = bp.Entities.Where(e => "pumpjack".Equals(e.Name)).ToDictionary(e => e.EntityNumber);
                var pumpjackIds = bp.Entities.Where(e => "pumpjack".Equals(e.Name)).Select(p => p.EntityNumber).ToList();
                int randomizeAmount = rng.Next(5);
                for (int i = 0; i <= randomizeAmount; i++)
                {
                    int id = rng.Next(pumpjackIds.Count);
                    int direction = rng.Next(4) * 2;
                    pumpjackIdMap[pumpjackIds[id]].Direction = direction;
                }
                var test = LayPipes(bp, useSpeed3, useBeacons);
                double testFitness = test.extraData.Fitness;

                if (testFitness > bestFitness)
                {
                    Console.WriteLine("Found layout with " + test.extraData.PipeCount + " pipes after " + iterationsWithoutImprovement + " iterations.");
                    bestFitness = testFitness;
                    bestBp = bp;
                    bestFinishedBp = test;
                    iterationsWithoutImprovement = 0;
                }
            }
            
            Clipboard.SetText(bestFinishedBp.ExportBlueprintString());
        }
    }
}
