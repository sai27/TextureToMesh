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

public class Face
{
    public Face(int v1, int v2, int v3)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;
    }

    public int v1;
    public int v2;
    public int v3;
}

public enum Channel
{
    Zero,
    Half,
    One,
}

public struct Pixel
{
    public Channel alpha;
    public bool cliped;
}

public class Rectangle
{
    public int xMin;
    public int yMin;
    public int xMax;
    public int yMax;

    public Rectangle(int xMin, int yMin, int xMax, int yMax)
    {
        this.xMin = xMin;
        this.yMin = yMin;
        this.xMax = xMax;
        this.yMax = yMax;
    }

    public int Area
    {
        get
        {
            return (xMax - xMin + 1) * (yMax - yMin + 1);
        }
    }
        
}
public class PixelRaw
{
    public PixelRaw(Color[] colors, int width, int height)
    {
        m_width = width;
        m_height = height;
        m_raws = new Pixel[height, width];
        m_matrix = new int[height, width];

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                Color c = colors[i * height + j];
                if (c.a <= 0.05f)
                {
                    m_raws[i, j].cliped = true;
                    m_raws[i, j].alpha = Channel.Zero;
                    
                }
                else if (c.a >= 0.95f)
                {
                    m_raws[i, j].cliped = false;
                    m_raws[i, j].alpha = Channel.One;
                }
                else
                {
                    m_raws[i, j].cliped = false;
                    m_raws[i, j].alpha = Channel.Half;
                }
            }
        }
    }

    public void ComputeOpaques()
    {
        while (!isComputeOver())
        {
            ComputeOpaque();
        }
        Debug.Log('a');
    }

    public void WriteObj(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (StreamWriter sw = new StreamWriter(fs))
        {
            Dictionary<int, int> verts = new Dictionary<int, int>(1024);

            for (int i = 0; i < m_opaques.Count; i++)
            {
                var rect = m_opaques[i];
                rect.xMax += 1;
                rect.yMax += 1;
                var key = rect.xMin << 16 | rect.yMin;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = rect.xMin << 16 | rect.yMax;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = rect.xMax << 16 | rect.yMax;
                if (!verts.ContainsKey(key))
                {
                    verts.Add(key, verts.Count + 1);
                }

                key = rect.xMax << 16 | rect.yMin;
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

            for (int i = 0; i < m_opaques.Count; i++)
            {
                var rect = m_opaques[i];
                var v1 = rect.xMin << 16 | rect.yMin;
                var v2 = rect.xMin << 16 | rect.yMax;
                var v3 = rect.xMax << 16 | rect.yMax;
                var v4 = rect.xMax << 16 | rect.yMin;

                sw.Write(string.Format("f {0} {1} {2}\n", verts[v1], verts[v2], verts[v3]));
                sw.Write(string.Format("f {0} {1} {2}\n", verts[v1], verts[v3], verts[v4]));
            }
        }
    }

    private bool isComputeOver()
    {
        for (int i = 0; i < m_height; i++)
        {
            for (int j = 0; j < m_width; j++)
            {
                if (m_raws[i, j].cliped == false)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void updateMatrix()
    {
        for (int i = m_height - 1; i >= 0; i--)
        {
            for (int j = 0; j < m_width; j++)
            {
                if (m_raws[i, j].cliped)
                {
                    m_matrix[i, j] = 0;
                }
                else
                {
                    if (i < m_height - 1)
                    {
                        m_matrix[i, j] = m_matrix[i + 1, j] + 1;
                    }
                    else
                    {
                        m_matrix[i, j] = 1;
                    }
                }
            }
        }
    }

    private void updateRaw(Rectangle rect)
    {
        for (int i = rect.yMin; i <= rect.yMax; i++)
        {
            for (int j = rect.xMin; j <= rect.xMax; j++)
            {
                m_raws[i, j].cliped = true;
            }
        }
    }

    private void ComputeOpaque()
    {
        updateMatrix();

        List<int> listHeight = new List<int>(m_width);
        Rectangle maxRect = null;
        for (int i = 0; i < m_height; i++)
        {
            listHeight.Clear();
            for (int j = 0; j < m_width; j++)
            {
                listHeight.Add(m_matrix[i, j]);
            }
            Rectangle rect = ComputeLargestRectangle(listHeight, i);
            if (rect == null)
                continue;

            if (maxRect == null)
            {
                maxRect = rect;
            }
            else
            {
                if (rect.Area > maxRect.Area)
                {
                    maxRect = rect;
                }
            }
        }

        updateRaw(maxRect);
        m_opaques.Add(maxRect);
    }

    private Rectangle ComputeLargestRectangle(List<int> listHeight, int yMin)   
    {
        Rectangle rect = null;
        Stack<int> stkHeightIdx = new Stack<int>();
        listHeight.Add(0);
        int maxArea = 0 ;
        for (int i = 0; i < listHeight.Count; )  
        {
            if (stkHeightIdx.Count == 0 || listHeight[i] > listHeight[stkHeightIdx.Peek()])  
            {
                stkHeightIdx.Push(i);
                i++;  
            }  
            else  
            {
                int index = stkHeightIdx.Peek();
                stkHeightIdx.Pop();
                int w = 0;
                if (stkHeightIdx.Count == 0)
                {
                    w = i;
                    int area = listHeight[index] * w;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        rect = new Rectangle(0, yMin, w - 1, yMin + listHeight[index] - 1);
                    }
                }
                else
                {
                    w = i - stkHeightIdx.Peek() - 1;
                    int area = listHeight[index] * w;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        rect = new Rectangle(stkHeightIdx.Peek() + 1, yMin, i - 1, yMin + listHeight[index] - 1);
                    }
                }
            }
        }  
        return rect;  
    }

    private Pixel[,]        m_raws = null;
    private int[,]          m_matrix = null; 
    private int             m_width;
    private int             m_height;
    private List<Rectangle> m_opaques = new List<Rectangle>(128);
}

public class TextureToMesh
{
    private static void GetMaxRect(Pixel[,] mat)
    {
    }

    [MenuItem("Tools/TextureToMesh")]
    public static void Transform()
    {
        Texture2D tex = Selection.activeObject as Texture2D;
        PixelRaw pr1 = new PixelRaw(tex.GetPixels(), tex.width, tex.height);
        pr1.ComputeOpaques();
        pr1.WriteObj("Assets/test1.obj");
        Debug.Log("a");
        return;

        Color[] colors = new Color[64];
        colors[2 * 8 + 2] = new Color(0, 0, 0, 1);
        colors[3 * 8 + 2] = new Color(0, 0, 0, 1);
        colors[4 * 8 + 2] = new Color(0, 0, 0, 1);
        colors[4 * 8 + 7] = new Color(0, 0, 0, 1);
        colors[5 * 8 + 3] = new Color(0, 0, 0, 1);
        colors[5 * 8 + 5] = new Color(0, 0, 0, 1);
        colors[5 * 8 + 6] = new Color(0, 0, 0, 1);
        colors[5 * 8 + 7] = new Color(0, 0, 0, 1);
        colors[6 * 8 + 5] = new Color(0, 0, 0, 1);
        colors[6 * 8 + 6] = new Color(0, 0, 0, 1);
        colors[6 * 8 + 7] = new Color(0, 0, 0, 1);
        colors[7 * 8 + 4] = new Color(0, 0, 0, 1);
        colors[7 * 8 + 5] = new Color(0, 0, 0, 1);
        colors[7 * 8 + 6] = new Color(0, 0, 0, 1);
        colors[7 * 8 + 7] = new Color(0, 0, 0, 1);

        for (int i = 0; i < 64; i++)
        {
            colors[i] = colors[i].a == 0 ? new Color(0, 0, 0, 1) : new Color(0, 0, 0, 0);
        }

        PixelRaw pr = new PixelRaw(colors, 8, 8);
        pr.ComputeOpaques();
        pr.WriteObj("Assets/test.obj");
        Debug.Log("a");
    }
}
