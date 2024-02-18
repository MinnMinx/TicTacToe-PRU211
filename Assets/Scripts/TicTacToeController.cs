using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TicTacToeController : MonoBehaviour
{
    [SerializeField]
    private GameObject xPrefab, oPrefab;
    [SerializeField]
    private ParticleSystem xFx, oFx;
    //[SerializeField]
    //private SpriteRenderer xPreview, oPreview;
    [SerializeField]
    private GridController gridCtrl;

    [SerializeField]
    private Material winnerLineMaterial;
    [SerializeField]
    private float winnerLineWidth;

    private Dictionary<int2, GameObject> xMoves = new Dictionary<int2, GameObject>();
    private Dictionary<int2, GameObject> oMoves = new Dictionary<int2, GameObject>();
    private List<LineRenderer> winnerLines = new List<LineRenderer>();

    private bool isOddTurn = true;
    private Result gameResult;
    private int minMoveToWin = 5;

    [SerializeField]
    private float ResetAfterSecond = 5f;
    private float timeUntilReset;
    // Start is called before the first frame update
    void Start()
    {
        StartGame();
    }

    void StartGame()
    {
        // Destroy all old objects
        if (xMoves.Count > 0)
        {
            foreach(var gameObj in xMoves.Values)
            {
                Destroy(gameObj);
            }
        }
        if (oMoves.Count > 0)
        {
            foreach (var gameObj in oMoves.Values)
            {
                Destroy(gameObj);
            }
        }
        if (winnerLines.Count > 0)
        {
            foreach (var line in winnerLines)
            {
                Destroy(line.gameObject);
            }
        }
        xMoves = new Dictionary<int2, GameObject>(gridCtrl.ColumnCount * gridCtrl.RowCount / 2 + 1);
        oMoves = new Dictionary<int2, GameObject>(gridCtrl.ColumnCount * gridCtrl.RowCount / 2 + 1);
        minMoveToWin = math.min(5, math.min(gridCtrl.RowCount, gridCtrl.ColumnCount));
        winnerLines.Clear();

        gameResult = Result.NOT_OVER;
        timeUntilReset = ResetAfterSecond;
        isOddTurn = true;
        if (oFx.isPlaying)
            oFx.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        oFx.Play();
        Debug.Log("Start: O's turn!");
    }

    // Update is called once per frame
    void Update()
    {
        if (gridCtrl.ColumnCount < 2 || gridCtrl.RowCount < 2)
        {
            Debug.Log("Column or row is < 2. Game won't be playable anymore");
            return;
        }
        if (gameResult == Result.NOT_OVER)
        {
            // Game continuing
            int2 gridScreenSize = new int2
            {
                x = Screen.width / gridCtrl.ColumnCount,
                y = Screen.height / gridCtrl.RowCount,
            };
            int2 mouseGridIndex = new int2
            {
                x = (int)(Input.mousePosition.y / gridScreenSize.y), // height -> row
                y = (int)(Input.mousePosition.x / gridScreenSize.x), // width -> column
            };

            // Check if mouse is on the screen
            if (mouseGridIndex.x >= 0 && mouseGridIndex.x < gridCtrl.RowCount &&
                mouseGridIndex.y >= 0 && mouseGridIndex.y < gridCtrl.ColumnCount)
            {
                // check for click
                if (Input.GetMouseButtonDown(0) &&
                    !xMoves.ContainsKey(mouseGridIndex) &&
                    !oMoves.ContainsKey(mouseGridIndex))
                {
                    // Create new object
                    MakeMove(mouseGridIndex);

                    // Check for draw
                    if (oMoves.Count + xMoves.Count == gridCtrl.ColumnCount * gridCtrl.RowCount)
                    {
                        // Draw
                        Debug.Log("Game Result: Draw!");
                        gameResult = Result.DRAW;
                        return;
                    }
                    else if (oMoves.Count > minMoveToWin - 1 || xMoves.Count > minMoveToWin - 1)
                    {
                        // Check for wins
                        NativeArray<int2> _Moves = new NativeArray<int2>(isOddTurn ? oMoves.Keys.ToArray() : xMoves.Keys.ToArray(), Allocator.TempJob);
                        NativeList<int2> _LinesIndex = new NativeList<int2>(4 * _Moves.Length, Allocator.TempJob);
                        var handle = _Moves.SortJob(new GridIndexComparer()).Schedule();

                        handle = new CheckForLineJob()
                        {
                            Moves = _Moves,
                            Lines = _LinesIndex.AsParallelWriter(),
                            minMoveForLine = this.minMoveToWin,
                        }.Schedule(_Moves.Length - 1, 4, handle);

                        handle.Complete();
                        if (_LinesIndex.Length > 0)
                        {
                            winnerLines.Capacity = _LinesIndex.Length;
                            foreach (var lineIndex in _LinesIndex)
                            {
                                GameObject lineObj = new GameObject("Line");
                                var lineRenderer = lineObj.AddComponent<LineRenderer>();
                                lineRenderer.material = winnerLineMaterial;
                                lineRenderer.widthMultiplier = winnerLineWidth;
                                lineRenderer.sortingOrder = 100;
                                lineRenderer.numCapVertices = 90;
                                lineRenderer.positionCount = 2;
                                lineRenderer.SetPosition(0, gridCtrl.GetPosition(_Moves[lineIndex.x].x, _Moves[lineIndex.x].y));
                                lineRenderer.SetPosition(1, gridCtrl.GetPosition(_Moves[lineIndex.y].x, _Moves[lineIndex.y].y));
                                winnerLines.Add(lineRenderer);
                            }

                            if (isOddTurn)
                            {
                                Debug.Log("Game Result: O has won!");
                                gameResult = Result.O_WIN;
                            }
                            else
                            {
                                Debug.Log("Game Result: X has won!");
                                gameResult = Result.X_WIN;
                            }
                            _Moves.Dispose();
                            _LinesIndex.Dispose();
                            return;
                        }
                        _Moves.Dispose();
                        _LinesIndex.Dispose();
                    }

                    //Switch Turn
                    isOddTurn = !isOddTurn;
                }
            }
        }
        else
        {
            RunResetTimer();
        }
    }
    
    void MakeMove(int2 gridIndex)
    {
        GameObject gameObj = Instantiate(isOddTurn ? oPrefab : xPrefab, transform);
        gameObj.transform.position = gridCtrl.GetPosition(gridIndex.x, gridIndex.y);
        gameObj.transform.localScale = new Vector3(gridCtrl.GridSize.x, gridCtrl.GridSize.y, 1);
        if (isOddTurn)
        {
            oMoves.Add(gridIndex, gameObj);
            if (xFx.isPlaying)
                xFx.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            oFx.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            xFx.Play();
        }
        else
        {
            xMoves.Add(gridIndex, gameObj);
            if (oFx.isPlaying)
                oFx.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            xFx.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            oFx.Play();
        }
    }

    void RunResetTimer()
    {
        timeUntilReset -= Time.deltaTime;
        if (timeUntilReset < 0)
        {
            StartGame();
        }
    }

    public enum Result
    {
        NOT_OVER,
        X_WIN,
        O_WIN,
        DRAW,
    }

    [BurstCompile]
    public struct CheckForLineJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int2> Moves;

        [ReadOnly]
        public int minMoveForLine;

        [WriteOnly]
        public NativeList<int2>.ParallelWriter Lines;

        public void Execute(int index)
        {
            // checking for horizontal line
            int count = 0;
            int endIndex = CheckHorizontalLine(index, ref count);
            if (count >= minMoveForLine - 1)
            {
                Lines.AddNoResize(new int2(index, endIndex));
            }

            // checking for diagonal (to the right) line
            count = 0;
            endIndex = CheckDiagonalRightLine(index, ref count);
            if (count >= minMoveForLine - 1)
            {
                Lines.AddNoResize(new int2(index, endIndex));
            }

            // checking for diagonal (to the left) line
            count = 0;
            endIndex = CheckDiagonalLeftLine(index, ref count);
            if (count >= minMoveForLine - 1)
            {
                Lines.AddNoResize(new int2(index, endIndex));
            }

            // checking for vertical line
            count = 0;
            endIndex = CheckVerticalLine(index, ref count);
            if (count >= minMoveForLine - 1)
            {
                Lines.AddNoResize(new int2(index, endIndex));
            }
        }

        int CheckVerticalLine(int index, ref int count)
        {
            for (int i = index + 1; i < Moves.Length; i++)
            {
                if (Moves[i].x == Moves[index].x + 1 &&
                    Moves[i].y == Moves[index].y)
                {
                    count++;
                    return CheckVerticalLine(i, ref count);
                }
            }
            return index;
        }

        int CheckDiagonalRightLine(int index, ref int count)
        {
            for (int i = index + 1; i < Moves.Length; i++)
            {
                if (Moves[i].y == Moves[index].y + 1 &&
                    Moves[i].x == Moves[index].x + 1)
                {
                    count++;
                    return CheckDiagonalRightLine(i, ref count);
                }
            }
            return index;
        }

        int CheckDiagonalLeftLine(int index, ref int count)
        {
            for (int i = index + 1; i < Moves.Length; i++)
            {
                if (Moves[i].y == Moves[index].y - 1 &&
                    Moves[i].x == Moves[index].x + 1)
                {
                    count++;
                    return CheckDiagonalLeftLine(i, ref count);
                }
            }
            return index;
        }

        int CheckHorizontalLine(int index, ref int count)
        {
            if (index < Moves.Length - 1 &&
                Moves[index + 1].x == Moves[index].x &&
                Moves[index + 1].y == Moves[index].y + 1)
            {
                count++;
                return CheckHorizontalLine(index + 1, ref count);
            }
            return index;
        }
    }

    public struct GridIndexComparer : IComparer<int2>
    {
        public int Compare(int2 x, int2 y)
        {
            // compare x then y
            int result = x.x.CompareTo(y.x);
            if (result == 0)
            {
                result = x.y.CompareTo(y.y);
            }
            return result;
        }
    }
}
