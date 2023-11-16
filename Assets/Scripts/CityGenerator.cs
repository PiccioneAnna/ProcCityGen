using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BlockDivision;
using BlockGeneration;
using GraphModel;
using MeshGeneration;
using RoadGeneration;
using Services;
using UnityEngine;

public class CityGenerator : MonoBehaviour
{
    #region Fields

    private Graph roadGraph; //Graph which will be built, and then drawn
    private List<BlockNode> blockNodes; //Nodes of the Blocks
    private List<Block> blocks;
    private List<Block> unoccupiedBlocks;
    private List<Block> thinnedBlocks;
    private List<BlockNode> thinnedBlockNodes;
    private List<Block> lots;
    private System.Random rand;

    private List<Block> concaveBlocks;
    private List<Block> convexBlocks;
    private List<BlockMesh> blockMeshes;
    private List<BlockMesh> lotMeshes;
    private List<BlockMesh> emptylotMeshes;
    private List<BoundingRectangle> boundingRectangles;
    private float blockHeight = 0.02f;

    [Header("Seed and Size")]
    public int mapSize = 300;
    public int seed = 13;

    [Header("Major Road generation")]
    [Range(0, 20)]
    public int maxMajDegreeInCurves = 10;
    [Range(0.01f, 1f)]
    public float majBranchingProb = 0.15f;
    [Range(0.01f, 1f)]
    public float majLeanProb = 0.25f;

    [Header("Minor Road generation")]
    [Range(0, 20)]
    public int maxMinDegreeInCurves = 10;
    [Range(0.01f, 1f)]
    public float minBranchingProb = 0.75f;
    [Range(0.01f, 1f)]
    public float minLeanProb = 0.75f;
    [Range(0.01f, 1f)]
    public float spawnOnMajorRoadsProb = 0.6f;

    [Header("Maximum Number of Roads")]
    public int maxMajorRoad = 2000;
    public int maxMinorRoad = 10000;

    [Header("Thickness of Roads")]
    [Range(0.1f, 2.5f)]
    public float majorThickness = 2.5f;
    [Range(0.1f, 2.5f)]
    public float minorThickness = 0.9f;

    [Header("Sidewalk generation")]
    [Range(0.1f, 1f)]
    public float sidewalkThickness = 0.5f;

    [Header("Building generation")]
    public float minBuildHeight = 2;
    public float maxBuildHeight = 15;

    [Header("Gizmos")]
    public bool drawRoadNodes;
    public bool drawRoads = true;
    public bool drawBlockNodes;
    public bool drawBlocks = true;
    public bool drawThinnedBlockNodes;
    public bool drawThinnedBlocks;
    public bool drawLots = true;
    public bool drawConvexBlocks;
    public bool drawConcaveBlocks;
    public bool drawTriangulatedMeshes;
    public bool drawBoundingBoxes;

    [Header("Materials")]
    public Material roadMaterial;
    public Material blockMaterial;
    public Material lotMaterial;
    public Material emptyLotMaterial;

    //Event to call, when the generation is ready
    private bool genReady;
    private bool genDone;

    //Time tracking
    private Stopwatch mainSw;
    private Stopwatch sw;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        rand = new System.Random(seed);
        roadGraph = new Graph();

        lots = new List<Block>();

        Thread t = new(ThreadProc);
        t.Start();
    }


    // Update is called once per frame
    void Update()
    {
        if (genReady && !genDone) //This make sure, that this will be only called once
        {
            genDone = true;
            GenerateGameObjects();
        }
    }

    private void ThreadProc()
    {
        mainSw = Stopwatch.StartNew();
        sw = Stopwatch.StartNew();

        GenerateRoads();
        GenerateBaseBlocks();
        GenerateLots();
        GenerateMeshes();

        mainSw.Stop();
        UnityEngine.Debug.Log("City generation time taken: " + mainSw.Elapsed.TotalMilliseconds + " ms");

        genReady = true;
    }

    #region 2D Generation Functions

    private void GenerateRoads()
    {
        //ROAD GENERATION
        MajorGenerator majorGen = new(
                rand, mapSize, maxMajorRoad, roadGraph, roadGraph.MajorNodes,
                roadGraph.MajorEdges, maxMajDegreeInCurves, majBranchingProb, majLeanProb);

        majorGen.Run();

        List<Segment> majorRoads = majorGen.GetRoadSegments();

        MinorGenerator minorGen = new(
                rand, mapSize, maxMinorRoad, roadGraph, roadGraph.MinorNodes,
                roadGraph.MinorEdges, maxMinDegreeInCurves, minBranchingProb, 
                minLeanProb, majorRoads, spawnOnMajorRoadsProb);

        minorGen.Run();

        //ROAD GENERATION TIME, ROAD COUNT
        sw.Stop();
        UnityEngine.Debug.Log("Road generation time taken: " + sw.Elapsed.TotalMilliseconds + " ms");
        UnityEngine.Debug.Log(majorGen.GetRoadSegments().Count + " major roads generated");
        UnityEngine.Debug.Log(minorGen.GetRoadSegments().Count + " minor roads generated");
    }

    private void GenerateBaseBlocks()
    {
        //BLOCK GENERATION
        BlockGenerator blockGen = new(roadGraph, mapSize, majorThickness, minorThickness, blockHeight);
        blockGen.Generate();
        blockNodes = blockGen.BlockNodes;
        blocks = blockGen.Blocks;
        unoccupiedBlocks = blockGen.UnoccupiedBlocks;
        UnityEngine.Debug.Log(blockGen.Blocks.Count + " block generated");

        GenerateSideWalks(blockGen);
    }

    private void GenerateSideWalks(BlockGenerator blockGen)
    {
        //SIDEWALK GENERATION
        blockGen.AddSidewalks(sidewalkThickness);
        thinnedBlocks = blockGen.ThinnedBlocks;
        thinnedBlockNodes = blockGen.ThinnedBlockNodes;
        UnityEngine.Debug.Log("Sidewalk generation completed");
    }

    private void GenerateLots() //Subdivided valid blocks
    {
        //BLOCK DIVISION
        sw = System.Diagnostics.Stopwatch.StartNew();

        BlockDivider blockDiv = new BlockDivider(rand, thinnedBlocks, lots);
        blockDiv.DivideBlocks();
        blockDiv.SetBuildingHeights(minBuildHeight, maxBuildHeight, blockHeight, mapSize);
        boundingRectangles = blockDiv.BoundingRectangles;
    }

    private void GenerateMeshes()
    {
        //BLOCK MESH GENERATION
        MeshGenerator blockMeshGen = new MeshGenerator(blocks, blockHeight);
        blockMeshGen.GenerateMeshes();
        blockMeshes = blockMeshGen.BlockMeshes;

        //LOT MESH GENERATION
        MeshGenerator lotMeshGen = new MeshGenerator(lots, blockHeight + blockHeight / 3);
        lotMeshGen.GenerateMeshes();

        MeshGenerator irregularlotMeshGen = new MeshGenerator(unoccupiedBlocks, blockHeight + blockHeight / 3);
        irregularlotMeshGen.GenerateMeshes();
        emptylotMeshes = irregularlotMeshGen.BlockMeshes;

        convexBlocks = lotMeshGen.ConvexBlocks;
        concaveBlocks = lotMeshGen.ConcaveBlocks;
        lotMeshes = lotMeshGen.BlockMeshes;
    }

    #endregion

    #region 3D Generation Functions
    private void GenerateGameObjects()
    {
        var separator = new GameObject
        {
            name = "==========="
        };

        var roadPlane = new GameObject
        {
            name = "Road Plane"
        };
        roadPlane.AddComponent<MeshFilter>();
        roadPlane.AddComponent<MeshRenderer>();
        roadPlane.GetComponent<MeshFilter>().mesh = MeshCreateService.GenerateRoadMesh(mapSize);
        roadPlane.GetComponent<MeshRenderer>().material = roadMaterial;

        //Make Blocks
        var blockContainer = new GameObject
        {
            name = "Block Container"
        };

        GenerateGameObjectMeshes(lotMeshes, blockContainer, "Building", blockMaterial, blockMaterial);

        //Make Lots
        var lotContainer = new GameObject
        {
            name = "Lot Container"
        };

        GenerateGameObjectMeshes(blockMeshes, lotContainer, "Lot", emptyLotMaterial, lotMaterial);

        //Make Lots
        var emptyLotContainer = new GameObject
        {
            name = "Empty Lot Container"
        };

        GenerateGameObjectMeshes(emptylotMeshes, emptyLotContainer, "Empty Lot", emptyLotMaterial, emptyLotMaterial);

    }
    private void GenerateGameObjectMeshes(List<BlockMesh> meshList, GameObject container, string name, Material materialOccupied, Material material)
    {
        for (int i = 0; i < meshList.Count; i++)
        {
            var temp = new GameObject
            {
                name = name
            };
            temp.transform.parent = container.transform;
            temp.AddComponent<MeshFilter>();
            temp.AddComponent<MeshRenderer>();
            temp.GetComponent<MeshFilter>().mesh = MeshCreateService.GenerateBlockMesh(meshList[i]);

            if (!meshList[i].Block.IsOccupied) temp.GetComponent<MeshRenderer>().material = materialOccupied;
            else temp.GetComponent<MeshRenderer>().material = material;
        }
    }

    #endregion

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (roadGraph == null)
        {
            return;
        }

        if (drawRoads)
        {
            GizmoService.DrawEdges(roadGraph.MajorEdges, Color.white);
            GizmoService.DrawEdges(roadGraph.MinorEdges, Color.black);
        }

        if (drawRoadNodes)
        {
            GizmoService.DrawNodes(roadGraph.MajorNodes, Color.white, 2f);
            GizmoService.DrawNodes(roadGraph.MinorNodes, Color.black, 1f);
        }

        if (drawBlockNodes)
        {
            GizmoService.DrawBlockNodes(blockNodes, Color.red, 0.4f);
        }

        if (drawBlocks)
        {
            GizmoService.DrawBlocks(blocks, Color.gray);
            GizmoService.DrawBlocks(unoccupiedBlocks, Color.grey);
        }

        if (drawThinnedBlocks)
        {
            GizmoService.DrawBlocks(thinnedBlocks, Color.green);
        }

        if (drawThinnedBlockNodes)
        {
            GizmoService.DrawBlockNodes(thinnedBlockNodes, Color.yellow, 0.4f);
        }

        if (drawLots)
        {
            GizmoService.DrawBlocks(lots, new Color(0.2f, 0.7f, 0.7f));
        }

        if (drawConvexBlocks && genDone)
        {
            GizmoService.DrawBlocks(convexBlocks, new Color(0.2f, 0.7f, 0.7f));
        }

        if (drawConcaveBlocks && genDone)
        {
            GizmoService.DrawBlocks(concaveBlocks, new Color(0.2f, 0.7f, 0.2f));
        }

        if (drawTriangulatedMeshes && genDone)
        {
            GizmoService.DrawBlockMeshes(blockMeshes, new Color(.8f, .8f, .8f));
        }

        if (drawBoundingBoxes && genDone)
        {
            List<Edge> cutEdges = new List<Edge>();

            foreach (var boundingBox in boundingRectangles)
            {
                GizmoService.DrawEdges(boundingBox.Edges, Color.white);
                cutEdges.Add(boundingBox.GetCutEdge());
            }

            GizmoService.DrawEdges(cutEdges, Color.yellow);
        }
    }
    #endregion
}
