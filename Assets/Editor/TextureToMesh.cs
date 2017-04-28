using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class Vertice
{
    public Vertice(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public int x;
    public int y;
}

public class MatrixSubset
{
    public class Item
    {
        public Item(float value, bool marked)
        {
            this.value = value;
            this.marked = marked;
        }

        public float value;
        public bool marked;
    }

    public class Quad
    {
        public int xMin;
        public int yMin;
        public int xMax;
        public int yMax;

        public Quad(int xMin, int yMin, int xMax, int yMax)
        {
            this.xMin = xMin;
            this.yMin = yMin;
            this.xMax = xMax;
            this.yMax = yMax;
        }

        public int Area { get { return (xMax - xMin + 1) * (yMax - yMin + 1); } }
    }

    public MatrixSubset(float[] raw, int width, int height, int minQuad)
    {
        m_width         = width;
        m_height        = height;
        m_rawMatrix     = new Item[height, width];
        m_idxMatrix     = new int[height, width];
        m_minQuad       = minQuad;
        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                var value = raw[h * height + w];
                var marked = false;
                if (value < 1.0f)
                {
                    marked = true;
                }
                var rawItem = new Item(value, marked);
                m_rawMatrix[h, w] = rawItem;
                m_idxMatrix[h, w] = 0;
            }
        }
    }

    public void ComputeQuads()
    {
        while (!isAllMarked())
        {
            ComputeMaxQuad();
        }
        DiscardMinimalQuad();
    }

    private bool isAllMarked()
    {
        for (int h = 0; h < m_height; h++)
        {
            for (int w = 0; w < m_width; w++)
            {
                if (!m_rawMatrix[h, w].marked)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void updateIndexMatrix()
    {
        for (int h = m_height - 1; h >= 0; h--)
        {
            for (int w = 0; w < m_width; w++)
            {
                var item = m_rawMatrix[h, w];
                if (item.marked)
                {
                    m_idxMatrix[h, w] = 0;
                }
                else
                {
                    if (h < m_height - 1)
                    {
                        m_idxMatrix[h, w] = m_idxMatrix[h + 1, w] + 1;
                    }
                    else
                    {
                        m_idxMatrix[h, w] = 1;
                    }
                }
            }
        }
    }

    private void updateRawMatrixMarks(Quad quad, bool marked)
    {
        for (int h = quad.yMin; h <= quad.yMax; h++)
        {
            for (int w = quad.xMin; w <= quad.xMax; w++)
            {
                m_rawMatrix[h, w].marked = marked;
            }
        }
    }

    private void ComputeMaxQuad()
    {
        updateIndexMatrix();

        List<int> row = new List<int>(m_width);
        Quad maxQuad = null;
        for (int h = 0; h < m_height; h++)
        {
            row.Clear();
            for (int w = 0; w < m_width; w++)
            {
                row.Add(m_idxMatrix[h, w]);
            }
            Quad quad = ComputeRowMaxQuad(row, h);
            if (quad == null)
                continue;

            if (maxQuad == null)
            {
                maxQuad = quad;
            }
            else
            {
                if (quad.Area > maxQuad.Area)
                {
                    maxQuad = quad;
                }
            }
        }

        updateRawMatrixMarks(maxQuad, true);
        m_quads.Add(maxQuad);
    }

    private Quad ComputeRowMaxQuad(List<int> row, int yMin)   
    {
        Quad quad = null;
        Stack<int> idxStack = new Stack<int>();
        row.Add(0);
        int maxArea = 0 ;
        for (int i = 0; i < row.Count; )  
        {
            if (idxStack.Count == 0 || row[i] > row[idxStack.Peek()])  
            {
                idxStack.Push(i);
                i++;  
            }  
            else  
            {
                int oldTop = idxStack.Peek();
                idxStack.Pop();
                int width = 0;
                if (idxStack.Count == 0)
                {
                    width = i;
                    int area = row[oldTop] * width;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        quad = new Quad(0, yMin, width - 1, yMin + row[oldTop] - 1);
                    }
                }
                else
                {
                    int curTop = idxStack.Peek();
                    width = i - curTop - 1;
                    int area = row[oldTop] * width;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        quad = new Quad(curTop + 1, yMin, i - 1, yMin + row[oldTop] - 1);
                    }
                }
            }
        }
        return quad;  
    }

    private void DiscardMinimalQuad()
    {
        var quads = new List<Quad>(128);
        for (int i = 0; i < m_quads.Count; i++)
        {
            var quad = m_quads[i];
            if (quad.Area <= m_minQuad)
            {
                updateRawMatrixMarks(quad, false);
            }
            else
            {
                quads.Add(quad);
            }
        }
        m_quads = quads;
    }

    public void Write(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (StreamWriter sw = new StreamWriter(fs))
        {
            Dictionary<int, int> verts = new Dictionary<int, int>(1024);

            for (int i = 0; i < m_quads.Count; i++)
            {
                var quad = m_quads[i];
                quad.xMax += 1;
                quad.yMax += 1;
                var key = quad.xMin << 16 | quad.yMin;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = quad.xMin << 16 | quad.yMax;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = quad.xMax << 16 | quad.yMax;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = quad.xMax << 16 | quad.yMin;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }
            }

            foreach (var kvp in verts)
            {
                float x = (float)(kvp.Key >> 16);
                float y = (float)(kvp.Key & 0x0000ffff);
                sw.Write(string.Format("v {0} {1} {2}\n", x, y, 0.0f));
            }

            for (int i = 0; i < m_quads.Count; i++)
            {
                var quad = m_quads[i];
                var v1 = quad.xMin << 16 | quad.yMin;
                var v2 = quad.xMin << 16 | quad.yMax;
                var v3 = quad.xMax << 16 | quad.yMax;
                var v4 = quad.xMax << 16 | quad.yMin;

                sw.Write(string.Format("f {0} {1} {2}\n", verts[v1], verts[v2], verts[v3]));
                sw.Write(string.Format("f {0} {1} {2}\n", verts[v1], verts[v3], verts[v4]));
            }
        }
    }

    private Item[,]         m_rawMatrix = null;
    private int[,]          m_idxMatrix = null; 
    private int             m_width;
    private int             m_height;
    private int             m_minQuad;
    private List<Quad>      m_quads = new List<Quad>(128);
}

public class TextureToMesh
{
    [MenuItem("Tools/TextureToMesh")]
    public static void Transform()
    {
        {
            Texture2D tex = Selection.activeObject as Texture2D;
            var colors = tex.GetPixels();
            var raw = new float[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                raw[i] = colors[i].a;
            }

            var matrixSubset = new MatrixSubset(raw, tex.width, tex.height, 4);
            matrixSubset.ComputeQuads();
            matrixSubset.Write("Assets/test2.obj");
            Debug.Log("a");
        }

        return;

        {
            var raw = new float[64];
            raw[2 * 8 + 2] = 1.0f;
            raw[3 * 8 + 2] = 1.0f;
            raw[4 * 8 + 2] = 1.0f;
            raw[4 * 8 + 7] = 1.0f;
            raw[5 * 8 + 3] = 1.0f;
            raw[5 * 8 + 5] = 1.0f;
            raw[5 * 8 + 6] = 1.0f;
            raw[5 * 8 + 7] = 1.0f;
            raw[6 * 8 + 5] = 1.0f;
            raw[6 * 8 + 6] = 1.0f;
            raw[6 * 8 + 7] = 1.0f;
            raw[7 * 8 + 4] = 1.0f;
            raw[7 * 8 + 5] = 1.0f;
            raw[7 * 8 + 6] = 1.0f;
            raw[7 * 8 + 7] = 1.0f;

            var matrixSubset = new MatrixSubset(raw, 8, 8, 0);
            matrixSubset.ComputeQuads();
            matrixSubset.Write("Assets/test.obj");
            Debug.Log("a");
        }
    }
}
