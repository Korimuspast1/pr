package lav.bottleflip.alav;

import java.util.ArrayList;
import java.util.List;

/**
 * Генератор 3D-моделей в коде: боксы, тела вращения (бутылка, лампы,
 * растения), диски. Каждой вершине — свой цвет: «текстуры» не нужны.
 */
final class MeshBuilder {

    private final List<Float> verts = new ArrayList<>();
    private final List<Short> indices = new ArrayList<>();

    private void vertex(float x, float y, float z,
                        float nx, float ny, float nz, float[] color) {
        verts.add(x);
        verts.add(y);
        verts.add(z);
        verts.add(nx);
        verts.add(ny);
        verts.add(nz);
        verts.add(color[0]);
        verts.add(color[1]);
        verts.add(color[2]);
    }

    /** Ось-выровненный бокс с корректными нормалями, центр (cx,cy,cz). */
    MeshBuilder box(float cx, float cy, float cz,
                    float w, float h, float d, float[] color) {
        float x0 = cx - w / 2f, x1 = cx + w / 2;
        float y0 = cy - h / 2, y1 = cy + h / 2;
        float z0 = cz - d / 2, z1 = cz + d / 2;
        // +z (перед)
        quad(new float[]{x0, y0, z1}, new float[]{x1, y0, z1},
                new float[]{x1, y1, z1}, new float[]{x0, y1, z1},
                new float[]{0, 0, 1}, color);
        // -z (зад)
        quad(new float[]{x1, y0, z0}, new float[]{x0, y0, z0},
                new float[]{x0, y1, z0}, new float[]{x1, y1, z0},
                new float[]{0, 0, -1}, color);
        // +x
        quad(new float[]{x1, y0, z1}, new float[]{x1, y0, z0},
                new float[]{x1, y1, z0}, new float[]{x1, y1, z1},
                new float[]{1, 0, 0}, color);
        // -x
        quad(new float[]{x0, y0, z0}, new float[]{x0, y0, z1},
                new float[]{x0, y1, z1}, new float[]{x0, y1, z0},
                new float[]{-1, 0, 0}, color);
        // +y (верх)
        quad(new float[]{x0, y1, z1}, new float[]{x1, y1, z1},
                new float[]{x1, y1, z0}, new float[]{x0, y1, z0},
                new float[]{0, 1, 0}, color);
        // -y (низ)
        quad(new float[]{x0, y0, z0}, new float[]{x1, y0, z0},
                new float[]{x1, y0, z1}, new float[]{x0, y0, z1},
                new float[]{0, -1, 0}, color);
        return this;
    }

    private void quad(float[] a, float[] b, float[] c, float[] d,
                      float[] n, float[] color) {
        short base = (short) (verts.size() / Mesh.FLOATS_PER_VERTEX);
        vertex(a[0], a[1], a[2], n[0], n[1], n[2], color);
        vertex(b[0], b[1], b[2], n[0], n[1], n[2], color);
        vertex(c[0], c[1], c[2], n[0], n[1], n[2], color);
        vertex(d[0], d[1], d[2], n[0], n[1], n[2], color);
        indices.add(base);
        indices.add((short) (base + 1));
        indices.add((short) (base + 2));
        indices.add(base);
        indices.add((short) (base + 2));
        indices.add((short) (base + 3));
    }

    /**
     * Тело вращения вокруг оси Y. profile: [кольца] каждое {r, y, color}.
     * Нормали вычисляются из наклона профиля — поверхность гладкая.
     */
    MeshBuilder lathe(float[][] profile, float[][] colors, int segments,
                      float cx, float cy, float cz) {
        int rings = profile.length;
        short[][] ringBase = new short[rings][segments + 1];
        for (int i = 0; i < rings; i++) {
            float r = profile[i][0];
            float y = profile[i][1];
            // наклон профиля между соседними кольцами
            float dr, dy;
            if (i == 0) {
                dr = profile[1][0] - r;
                dy = profile[1][1] - y;
            } else if (i == rings - 1) {
                dr = r - profile[i - 1][0];
                dy = y - profile[i - 1][1];
            } else {
                dr = profile[i + 1][0] - profile[i - 1][0];
                dy = profile[i + 1][1] - profile[i - 1][1];
            }
            // нормаль в плоскости (r,y): перпендикуляр к (dr,dy) наружу
            float nr = dy;
            float ny = -dr;
            float len = (float) Math.sqrt(nr * nr + ny * ny);
            if (len < 1e-6f) {
                nr = 1;
                ny = 0;
                len = 1;
            }
            nr /= len;
            ny /= len;
            float[] color = colors[Math.min(i, colors.length - 1)];
            for (int j = 0; j <= segments; j++) {
                double a = 2.0 * Math.PI * j / segments;
                float ca = (float) Math.cos(a);
                float sa = (float) Math.sin(a);
                ringBase[i][j] = (short) (verts.size() / Mesh.FLOATS_PER_VERTEX);
                vertex(cx + r * ca, cy + y, cz + r * sa,
                        nr * ca, ny, nr * sa, color);
            }
        }
        for (int i = 0; i < rings - 1; i++) {
            for (int j = 0; j < segments; j++) {
                short a0 = ringBase[i][j];
                short a1 = ringBase[i][j + 1];
                short b0 = ringBase[i + 1][j];
                short b1 = ringBase[i + 1][j + 1];
                indices.add(a0);
                indices.add(b0);
                indices.add(b1);
                indices.add(a0);
                indices.add(b1);
                indices.add(a1);
            }
        }
        return this;
    }

    /** Плоский диск (тень под бутылкой), нормаль вверх. */
    MeshBuilder disc(float cx, float cy, float cz, float radius,
                     float[] color, int segments) {
        short center = (short) (verts.size() / Mesh.FLOATS_PER_VERTEX);
        vertex(cx, cy, cz, 0, 1, 0, color);
        for (int j = 0; j <= segments; j++) {
            double a = 2.0 * Math.PI * j / segments;
            vertex(cx + radius * (float) Math.cos(a), cy,
                    cz + radius * (float) Math.sin(a), 0, 1, 0, color);
        }
        for (int j = 0; j < segments; j++) {
            indices.add(center);
            indices.add((short) (center + 1 + j));
            indices.add((short) (center + 2 + j));
        }
        return this;
    }

    Mesh build() {
        float[] v = new float[verts.size()];
        for (int i = 0; i < v.length; i++) {
            v[i] = verts.get(i);
        }
        short[] ix = new short[indices.size()];
        for (int i = 0; i < ix.length; i++) {
            ix[i] = indices.get(i);
        }
        return new Mesh(v, ix);
    }
}
