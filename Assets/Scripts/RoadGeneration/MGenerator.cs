using System;
using System.Collections.Generic;
using GraphModel;
using UnityEngine;

#region reference
/// https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/RoadGeneration/MinorGenerator.cs
/// https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/RoadGeneration/MajorGenerator.cs
#endregion
namespace RoadGeneration
{
    /// <summary>
    /// Base class for major & minor road generation, handles shared behavior and architecture
    /// </summary>
    public class MGenerator
    {
        #region Fields
        // Ref to base data lists
        protected readonly List<Segment> globalGoalsRoads;
        protected readonly Queue<Segment> queue;
        protected readonly List<Segment> segments;

        // Ref to proper graph lists
        protected readonly List<Node> refNodes;
        protected readonly List<Edge> refEdges;

        // Ref to static universal values
        protected readonly System.Random rand;
        protected readonly int border;
        protected readonly int maxSegment;

        protected readonly float maxAngle;
        protected readonly float branchProb;
        protected readonly float leanProb;

        protected readonly Graph graph;

        protected const int RoadLength = 10;
        #endregion

        #region Default Constructor
        public MGenerator(System.Random seededRandom, int mapSize, int maxRoad, 
            Graph graphToBuild, List<Node> nodes, List<Edge> edges, float maxDegree, float branProb, float leannProb)
        {
            globalGoalsRoads = new List<Segment>();
            queue = new Queue<Segment>();
            segments = new List<Segment>();

            refNodes = nodes;
            refEdges = edges;

            maxAngle = maxDegree;
            branchProb = branProb;
            leanProb = leannProb;

            rand = seededRandom;
            border = mapSize;
            maxSegment = maxRoad;

            graph = graphToBuild;
        }
        #endregion

        #region Virtual Methods (overloaded by each generator's unique behavior)
        public virtual void Run() { }
        public virtual void GenerateStartSegments() { }
        public virtual bool CheckLocalConstraints(Segment segment) { return false; }
        public virtual void GlobalGoals(Segment segment) { }
        #endregion

        // Shared Behaviors (not overloaded, only defined in this base class)

        #region Generation Handlers

        /// <summary>
        /// Method runs until there is no segment left in the queue (mas segments is reached),
        /// Applys all relevant constraints and populates class's list data & GraphModel
        /// </summary>
        protected void SegmentGeneration()
        {
            while (queue.Count != 0 && segments.Count < maxSegment)
            {
                Segment current = queue.Peek();
                queue.Dequeue();

                if (!CheckLocalConstraints(current)) continue;

                segments.Add(current);

                AddToGraph(current);

                GlobalGoals(current);
            }
        }

        /// <summary>
        /// Method branches out from a segment, determining either right, left, or both
        /// </summary>
        /// <param name="segment"></param>
        protected void Branch(Segment segment)
        {
            Vector2 dirVector = segment.GetDirVector();

            int maxInt = (int)Math.Round((float)3 / branchProb);

            int branchRandom = rand.Next(0, maxInt);

            Vector2 normalVector1 = new Vector2(dirVector.y, -dirVector.x);
            Vector2 normalVector2 = new Vector2(-dirVector.y, dirVector.x);
            Segment branchedSegment1, branchedSegment2;

            switch (branchRandom)
            {
                // Branch to RIGHT ->
                case 1:
                    branchedSegment1 = CalcNewRoadSegment(segment.NodeTo, normalVector1);
                    globalGoalsRoads.Add(branchedSegment1);
                    break;
                // Branch to LEFT <-
                case 2:
                    branchedSegment2 = CalcNewRoadSegment(segment.NodeTo, normalVector2);
                    globalGoalsRoads.Add(branchedSegment2);
                    break;
                // Branch to BOTH <-->
                case 3:
                    branchedSegment1 = CalcNewRoadSegment(segment.NodeTo, normalVector1);
                    branchedSegment2 = CalcNewRoadSegment(segment.NodeTo, normalVector2);
                    globalGoalsRoads.Add(branchedSegment1);
                    globalGoalsRoads.Add(branchedSegment2);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Method determines a segments next segment (mostly for direction/angle calc)
        /// </summary>
        /// <param name="segment"></param>
        protected Segment GetContinuingRoadSegment(Segment segment)
        {
            Vector2 dirVector = segment.GetDirVector();
            Vector2 newDirVector;

            // decides whether to keep the current lean direction or randomly pick a new one
            bool newLeanDirection = KeepLean();

            // randomly pick new direction
            if (newLeanDirection)
            {
                dirVector = RotateVector(dirVector, GetRandomAngle(-2, (int)maxAngle * 2));
            }
            // stay in the same direction
            else
            {
                if (segment.LeanLeft)
                {
                    newDirVector = RotateVector(dirVector, GetRandomAngle(2, (int)maxAngle));
                    Segment segment1 = CalcNewRoadSegment(segment.NodeTo, newDirVector);
                    segment1.LeanLeft = true;
                    return segment1;
                }
                else if (segment.LeanRight)
                {
                    newDirVector = RotateVector(dirVector, GetRandomAngle(-2, -(int)maxAngle));
                    Segment segment1 = CalcNewRoadSegment(segment.NodeTo, newDirVector);
                    segment1.LeanRight = true;
                    return segment1;
                }
            }

            return CalcNewRoadSegment(segment.NodeTo, dirVector);
        }

        #endregion

        #region Checks

        /// <summary>
        /// Method checks if a segment follows a list of shared rules between both major & minor roads
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        protected bool CheckSharedConstraints(Segment segment)
        {
            // Check : Segment out of border
            if (segment.NodeFrom.X > border || segment.NodeFrom.X < -border
                || segment.NodeFrom.Y > border || segment.NodeFrom.Y < -border) return false;

            // Check : Segment overlaps self
            if (segment.NodeFrom.X == segment.NodeTo.X && segment.NodeFrom.Y == segment.NodeTo.Y) return false;

            // Check : To or From Node has more then 4 edges (ignoring crossroads with more bc they suck irl)
            if (segment.NodeTo.Edges.Count >= 4 || segment.NodeFrom.Edges.Count >= 4) return false;

            return true; // true if segment follows all rules
        }

        /// <summary>
        /// Method checks to see if max amount of segments has been reached
        /// </summary>
        /// <returns></returns>
        protected bool CheckMaxSegments()
        {
            return (segments.Count == maxSegment);
        }

        /// <summary>
        /// Method checks if a segment crosses an existing list of segments
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="existingSegments"></param>
        /// <returns></returns>
        protected bool CheckCrossing(Segment segment, List<Segment> existingSegments)
        {
            foreach (Segment road in existingSegments)
            {
                if (segment.IsCrossing(road)) return false;
            }
            return true;
        }

        /// <summary>
        /// Method checks if Node a is close to Node b
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        protected bool IsCloseToOtherNode(Node a, Node b)
        {
            float idealRadius = RoadLength * 0.8f;
            if (a.GetDistance(b) < idealRadius) return true;
            return false;
        }

        /// <summary>
        /// Method checks if Node is close to map's edge
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected bool IsCloseToMapEdge(Node node)
        {
            return node.X >= (border - 2) || node.X <= (-border + 2)
                || node.Y >= (border - 2) || node.Y <= (-border + 2);
        }

        #endregion

        #region Helper Methods(mostly for calculations & data fetching/storing)

        /// <summary>
        /// Method calculates a new road segment based off of origin node, direction vector, and defined lean
        /// </summary>
        /// <param name="nodeFrom"></param>
        /// <param name="dirVector"></param>
        /// <param name="leanIteration"></param>
        /// <returns></returns>
        protected Segment CalcNewRoadSegment(Node nodeFrom, Vector2 dirVector)
        {
            var newNodeTo = new Node(nodeFrom.X + dirVector.normalized.x * RoadLength, nodeFrom.Y + dirVector.normalized.y * RoadLength);
            return new Segment(nodeFrom, newNodeTo);
        }

        /// <summary>
        /// Method adds a road segment to graph, takes either major or minor data (graph.major/minor) depending on paramaters passed in
        /// </summary>
        /// <param name="road"></param>
        /// <param name="nodes"></param>
        /// <param name="edges"></param>
        protected void AddToGraph(Segment road)
        {
            if (!refNodes.Contains(road.NodeFrom) && !refNodes.Contains(road.NodeFrom)) refNodes.Add(road.NodeFrom);
            if (!refNodes.Contains(road.NodeTo) && !refNodes.Contains(road.NodeTo)) refNodes.Add(road.NodeTo);
            refEdges.Add(new Edge(road.NodeFrom, road.NodeTo));
        }

        /// <summary>
        /// Method returns a list of road segments
        /// </summary>
        /// <returns></returns>
        public List<Segment> GetRoadSegments()
        {
            return segments;
        }

        /// <summary>
        /// Gets random angle between a & b, returns in radians
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        protected float GetRandomAngle(int a, int b)
        {
            //First we make 'a' smaller, and generate a random number in the range
            if (b < a)
            {
                (b, a) = (a, b);
            }
            int range = Math.Abs(b - a);
            int rotation = rand.Next(0, range) + a;

            //then we make it to radian, and return it
            float rotationAngle = (float)(Math.PI / 180) * rotation;
            return rotationAngle;
        }

        /// <summary>
        /// Returns a rotated angle based on a starting direction and provided angle
        /// </summary>
        /// <param name="dirVector"></param>
        /// <param name="rotationAngle"></param>
        /// <returns></returns>
        protected Vector2 RotateVector(Vector2 dirVector, float rotationAngle)
        {
            //This works like a rotation matrix
            dirVector.x = ((float)Math.Cos(rotationAngle) * dirVector.x) - ((float)Math.Sin(rotationAngle) * dirVector.y);
            dirVector.y = ((float)Math.Sin(rotationAngle) * dirVector.x) + ((float)Math.Cos(rotationAngle) * dirVector.y);

            return dirVector;
        }

        /// <summary>
        /// Determine whether to keep a lean based on the prob the lean will be kept
        /// </summary>
        /// <returns></returns>
        protected bool KeepLean()
        {
            return rand.NextDouble() < leanProb;
        }

        #endregion

    }
}

