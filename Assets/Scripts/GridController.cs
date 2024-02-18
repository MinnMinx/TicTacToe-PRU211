using UnityEngine;

public class GridController : MonoBehaviour
{
    [SerializeField]
    private int widthCount;
    public int ColumnCount => widthCount;

    [SerializeField]
    private int heightCount;
    public int RowCount => heightCount;

    [SerializeField]
    private LineRenderer line;

    private Vector2 gridSize;
    public Vector2 GridSize => gridSize;

    private Vector3[] gridPositions;

    void Refresh()
    {
        var bottomLeft = Camera.main.ViewportToWorldPoint(Vector2.zero);
        var topRight = Camera.main.ViewportToWorldPoint(Vector2.one);
        bottomLeft.z = 0;
        topRight.z = 0;

        gridSize.x = Mathf.Abs(topRight.x - bottomLeft.x) / widthCount;
        gridSize.y = Mathf.Abs(topRight.y - bottomLeft.y) / heightCount;

        gridPositions = new Vector3[heightCount * widthCount];

        // n parts have (n + 1) lines
        // 2 point for each line
        var lines = new Vector3[heightCount * 2 + widthCount * 2 + 4];
        bool isLineEnd = false;
        for (int row = 0; row < heightCount; row++)
        {
            for (int col = 0; col < widthCount; col++)
            {
                gridPositions[col + row * widthCount] = new Vector3()
                {
                    x = bottomLeft.x + (col + 0.5f) * gridSize.x,
                    y = bottomLeft.y + (row + 0.5f) * gridSize.y
                };
            }

            // Fill a horizontal line
            lines[row * 2] = new Vector3()
            {
                x = isLineEnd ? bottomLeft.x : topRight.x,
                y = bottomLeft.y + gridSize.y * row,
            };
            lines[row * 2 + 1] = new Vector3()
            {
                x = isLineEnd ? topRight.x : bottomLeft.x,
                y = bottomLeft.y + gridSize.y * row,
            };
            isLineEnd = !isLineEnd;
        }
        // Fill extra horizontal line at the end
        lines[heightCount * 2] = new Vector3()
        {
            x = isLineEnd ? bottomLeft.x : topRight.x,
            y = bottomLeft.y + gridSize.y * heightCount,
        };
        lines[heightCount * 2 + 1] = new Vector3()
        {
            x = isLineEnd ? topRight.x : bottomLeft.x,
            y = bottomLeft.y + gridSize.y * heightCount,
        };

        isLineEnd = false;
        // Fill vertical lines
        for (int col = 0; col <= widthCount; col++)
        {
            lines[(heightCount + 1) * 2 + col * 2] = new Vector3()
            {
                x = bottomLeft.x + gridSize.x * col,
                y = isLineEnd ? bottomLeft.y : topRight.y,
            };
            lines[(heightCount + 1) * 2 + col * 2 + 1] = new Vector3()
            {
                x = bottomLeft.x + gridSize.x * col,
                y = isLineEnd ? topRight.y : bottomLeft.y,
            };
            isLineEnd = !isLineEnd;
        }

        // set position for line renderer
        if (line != null)
        {
            line.positionCount = lines.Length;
            line.SetPositions(lines);
        }
    }

    private void Start()
    {
        Refresh();
    }

    public Vector3 GetPosition(int row, int col)
    {
        if (row < 0 || col < 0 || row >= heightCount || col >= widthCount)
        {
            Debug.Log($"row: {row}, col: {col} is invalid");
            return gridPositions[0];
        }
        return gridPositions[col + row * widthCount];
    }
}
