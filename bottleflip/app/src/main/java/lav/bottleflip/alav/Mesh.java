package lav.bottleflip.alav;

import android.opengl.GLES20;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.nio.ShortBuffer;

/** Interleaved mesh: pos(3) + normal(3) + color(3), индексы short. */
final class Mesh {

    static final int FLOATS_PER_VERTEX = 9;
    static final int STRIDE = FLOATS_PER_VERTEX * 4;

    private final FloatBuffer vertexBuffer;
    private final ShortBuffer indexBuffer;
    private final int indexCount;

    Mesh(float[] vertices, short[] indices) {
        vertexBuffer = ByteBuffer.allocateDirect(vertices.length * 4)
                .order(ByteOrder.nativeOrder()).asFloatBuffer();
        vertexBuffer.put(vertices);
        vertexBuffer.position(0);
        indexBuffer = ByteBuffer.allocateDirect(indices.length * 2)
                .order(ByteOrder.nativeOrder()).asShortBuffer();
        indexBuffer.put(indices);
        indexBuffer.position(0);
        indexCount = indices.length;
    }

    void draw(int aPos, int aNormal, int aColor) {
        vertexBuffer.position(0);
        GLES20.glVertexAttribPointer(aPos, 3, GLES20.GL_FLOAT, false,
                STRIDE, vertexBuffer);
        vertexBuffer.position(3);
        GLES20.glVertexAttribPointer(aNormal, 3, GLES20.GL_FLOAT, false,
                STRIDE, vertexBuffer);
        vertexBuffer.position(6);
        GLES20.glVertexAttribPointer(aColor, 3, GLES20.GL_FLOAT, false,
                STRIDE, vertexBuffer);
        GLES20.glEnableVertexAttribArray(aPos);
        GLES20.glEnableVertexAttribArray(aNormal);
        GLES20.glEnableVertexAttribArray(aColor);
        indexBuffer.position(0);
        GLES20.glDrawElements(GLES20.GL_TRIANGLES, indexCount,
                GLES20.GL_UNSIGNED_SHORT, indexBuffer);
    }
}
