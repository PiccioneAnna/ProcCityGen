using GraphModel;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region reference
/// https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/RoadGeneration/MinorGenerator.cs
#endregion
namespace RoadGeneration
{
    public class MinorGenerator : MGenerator
    {
        #region Fields
        List<Segment> majorSegments;
        private float startSegmentProb;
        #endregion

        #region Constructor
        public MinorGenerator(System.Random seededRandom, int mapSize, int maxRoad, Graph graphToBuild,
            List<Node> nodes, List<Edge> edges, float maxDegree, float branProb, float leannProb, List<Segment> majorRoads, float spawnMajProb)
            : base(seededRandom, mapSize, maxRoad, graphToBuild, nodes, edges, maxDegree, branProb, leannProb)
        {
            majorSegments = majorRoads;
            startSegmentProb = spawnMajProb;
        }
        #endregion

        #region Overridden Base Functions
        public override void Run() 
        {
            GenerateStartSegments();

            SegmentGeneration();

            DataCleanUp();

            if (segments.Count == maxSegment) Debug.Log("Minor Roads reached maximal amount");
        }
        public override void GenerateStartSegments() 
        {
            foreach (Segment segment in majorSegments)
            {
                if (segment.EndSegment) continue;

                if (rand.NextDouble() < startSegmentProb) continue; // makes it so that a substreet doesn't spawn every major node

                var dirVector = segment.GetDirVector();
                var normalVector1 = new Vector2(dirVector.y, -dirVector.x);
                var normalVector2 = new Vector2(-dirVector.y, dirVector.x);

                Segment branchedSegment2 = CalcNewRoadSegment(segment.NodeTo, normalVector1);
                Segment branchedSegment3 = CalcNewRoadSegment(segment.NodeTo, normalVector2);
                queue.Enqueue(branchedSegment2);
                queue.Enqueue(branchedSegment3);
            }
        }
        public override bool CheckLocalConstraints(Segment segment) 
        {
            CheckForCloseNodes(segment);

            if (!CheckCrossings(segment)) { return false; }

            if (!CheckSharedConstraints(segment)) { return false; }

            if (!CheckNodeFreedom(segment)) { return false; }

            return true; 
        }
        public override void GlobalGoals(Segment segment) 
        {
            if (segment.EndSegment) return;

            globalGoalsRoads.Clear();

            Branch(segment);

            globalGoalsRoads.Add(GetContinuingRoadSegment(segment));

            foreach (Segment newSegment in globalGoalsRoads)
            {
                queue.Enqueue(newSegment);
            }
        }
        #endregion

        #region Helper Functions (Child class specific)

        #region Additional Checks
        /// <summary>
        /// Method checks to see if segment is crossing another
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private bool CheckCrossings(Segment segment)
        {
            foreach (Segment road in segments) //first check majorNodes
            {
                if (segment.IsCrossing(road)) return false;
            }

            foreach (Segment road in segments) //then check minorNodes
            {
                if (segment.IsCrossing(road)) return false;
            }

            return true;
        }

        /// <summary>
        /// Method checks to see if both a segments nodes are free
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private bool CheckNodeFreedom(Segment segment)
        {
            if (!segment.NodeFrom.IsFree(
                Mathf.Atan2(segment.NodeTo.Y - segment.NodeFrom.Y, segment.NodeTo.X - segment.NodeFrom.X)
                )) return false;  //direction is not free from NodeFrom

            if (!segment.NodeTo.IsFree(
                Mathf.Atan2(segment.NodeFrom.Y - segment.NodeTo.Y, segment.NodeFrom.X - segment.NodeTo.X)
                )) return false;  //direction is not free from NodeTo

            return true;
        }

        /// <summary>
        /// Method checks to see if there are any close nodes to fix a segment to
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private void CheckForCloseNodes(Segment segment)
        {
            //TRANSFORMATION

            foreach (Segment road in majorSegments) //first check majorNodes
            {
                if (IsCloseToOtherNode(segment.NodeTo, road.NodeTo))
                {
                    segment.NodeTo = road.NodeTo;
                    segment.EndSegment = true;
                }
            }

            foreach (Segment road in segments) //then check minorNodes
            {
                if (IsCloseToOtherNode(segment.NodeTo, road.NodeTo))
                {
                    segment.NodeTo = road.NodeTo;
                    segment.EndSegment = true;
                }
            }
        }
        #endregion

        #region Cleanup : Random Deletion

        private void DataCleanUp()
        {
            DeleteInsideLeaves();
            DeleteAloneEdges();
            DeleteAloneNodes();
        }

        private void DeleteAloneEdges()
        {
            List<Edge> removable = new();

            foreach (Edge edge in graph.MinorEdges)
            {
                if (edge.NodeA.Edges.Count == 1 && edge.NodeB.Edges.Count == 1)
                {
                    removable.Add(edge);
                }
            }

            foreach (Edge edge in removable)
            {
                graph.MinorEdges.Remove(edge);
                edge.NodeA.Edges.Remove(edge);
                edge.NodeB.Edges.Remove(edge);
            }

            //Leftover nodes will be deleted by DeleteAloneNodes()
        }

        private void DeleteAloneNodes()
        {
            List<Node> removable = new();

            foreach (Node node in graph.MinorNodes)
            {
                if (node.Edges.Count <= 0)
                {
                    removable.Add(node);
                }
            }

            foreach (Node node in removable)
            {
                graph.MinorNodes.Remove(node);
            }
        }

        private void DeleteInsideLeaves()
        {
            List<Node> removableNodes = new();
            List<Edge> removableEdges = new();

            foreach (Node node in graph.MinorNodes)
            {
                if (node.Edges.Count == 1 && !IsCloseToMapEdge(node)) //We only remove leaves which are not in the edge of the map
                {
                    removableNodes.Add(node);
                    if (!removableEdges.Contains(node.Edges[0])) removableEdges.Add(node.Edges[0]);
                }
            }

            foreach (Edge edge in removableEdges) //Remove the edges from other nodes and from the minorEdges list
            {
                foreach (Node node in graph.MinorNodes)
                {
                    if (node.Edges.Contains(edge)) node.Edges.Remove(edge);
                }
                foreach (Node node in graph.MajorNodes)
                {
                    if (node.Edges.Contains(edge)) node.Edges.Remove(edge);
                }
                graph.MinorEdges.Remove(edge);
            }

            foreach (Node node in removableNodes) //Remove the node from the minorNodes list
            {
                graph.MinorNodes.Remove(node);
            }
        }
        #endregion

        #endregion
    }
}

