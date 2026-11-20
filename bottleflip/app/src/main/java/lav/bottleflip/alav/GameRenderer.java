package lav.bottleflip.alav;

import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.opengl.Matrix;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** GL ES 2.0 рендерер: направленный свет + ambient, камера следует за бутылкой. */
final class GameRenderer implements GLSurfaceView.Renderer {

    private static final String VERTEX =
            "uniform mat4 uMVP;\n"
            + "attribute vec3 aPos;\n"
            + "attribute vec3 aNormal;\n"
            + "attribute vec3 aColor;\n"
            + "varying vec3 vColor;\n"
            + "varying vec3 vNormal;\n"
            + "void main() {\n"
            + "  vColor = aColor;\n"
            + "  vNormal = aNormal;\n"
            + "  gl_Position = uMVP * vec4(aPos, 1.0);\n"
            + "}\n";

    private static final String FRAGMENT =
            "precision mediump float;\n"
            + "varying vec3 vColor;\n"
            + "varying vec3 vNormal;\n"
            + "uniform vec3 uLightDir;\n"
            + "uniform float uAlpha;\n"
            + "void main() {\n"
            + "  vec3 n = normalize(vNormal);\n"
            + "  float diff = max(dot(n, normalize(uLightDir)), 0.0);\n"
            + "  vec3 c = vColor * (0.38 + 0.72 * diff);\n"
            + "  c += vColor * 0.10 * max(n.y, 0.0);\n"
            + "  gl_FragColor = vec4(c, uAlpha);\n"
            + "}\n";

    private final Game game;
    private int program;
    private int uMVP, uLightDir, uAlpha;
    private int aPos, aNormal, aColor;

    private Mesh bottle, shadow, floor, wall;
    private final Mesh[] furnitureMeshes = new Mesh[Models.TYPE_COUNT];

    private final float[] proj = new float[16];
    private final float[] view = new float[16];
    private final float[] model = new float[16];
    private final float[] mvp = new float[16];
    private final float[] tmp = new float[16];
    private float camX;
    private long lastNs;

    GameRenderer(Game game) {
        this.game = game;
        this.camX = game.bottleX;
    }

    @Override
    public void onSurfaceCreated(GL10 gl, EGLConfig config) {
        GLES20.glClearColor(0.55f, 0.72f, 0.92f, 1f);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glEnable(GLES20.GL_CULL_FACE);
        GLES20.glCullFace(GLES20.GL_BACK);
        program = buildProgram(VERTEX, FRAGMENT);
        uMVP = GLES20.glGetUniformLocation(program, "uMVP");
        uLightDir = GLES20.glGetUniformLocation(program, "uLightDir");
        uAlpha = GLES20.glGetUniformLocation(program, "uAlpha");
        aPos = GLES20.glGetAttribLocation(program, "aPos");
        aNormal = GLES20.glGetAttribLocation(program, "aNormal");
        aColor = GLES20.glGetAttribLocation(program, "aColor");
        bottle = Models.bottle();
        shadow = Models.shadow();
        floor = Models.floor();
        wall = Models.wall();
        for (int i = 0; i < Models.TYPE_COUNT; i++) {
            furnitureMeshes[i] = Models.furniture(i);
        }
        lastNs = System.nanoTime();
    }

    @Override
    public void onSurfaceChanged(GL10 gl, int width, int height) {
        GLES20.glViewport(0, 0, width, height);
        float aspect = (float) width / Math.max(1, height);
        Matrix.perspectiveM(proj, 0, 55f, aspect, 0.1f, 80f);
    }

    @Override
    public void onDrawFrame(GL10 gl) {
        long now = System.nanoTime();
        float dt = Math.min(0.05f, (now - lastNs) / 1e9f);
        lastNs = now;
        game.update(dt);

        // плавная камера
        float targetX = game.bottleX;
        camX += (targetX - camX) * Math.min(1f, dt * 5.5f);
        Matrix.setLookAtM(view, 0,
                camX - 2.6f, 2.5f, 4.7f,
                camX + 0.9f, 1.05f, 0f,
                0f, 1f, 0f);

        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
        GLES20.glUseProgram(program);
        GLES20.glUniform3f(uLightDir, -0.4f, 1.0f, 0.55f);
        GLES20.glUniform1f(uAlpha, 1f);

        // комната
        drawMesh(floor, camX, 0, 0, 1f, 0f);
        drawMesh(wall, camX, 0, 0, 1f, 0f);

        // платформы (только видимые)
        for (Game.Platform p : game.platforms) {
            if (Math.abs(p.centerX - camX) > 18f) {
                continue;
            }
            float top = game.platformTop(p);
            float depth = Models.depthOf(p.type);
            drawMesh(furnitureMeshes[p.type], p.centerX, top - 0f, 0,
                    p.width, 0f);
        }

        // тень
        GLES20.glEnable(GLES20.GL_BLEND);
        GLES20.glBlendFunc(GLES20.GL_SRC_ALPHA, GLES20.GL_ONE_MINUS_SRC_ALPHA);
        float surf = game.surfaceUnder();
        float height = Math.max(0f, game.bottleY - surf);
        float k = Math.max(0.35f, 1.1f - height * 0.25f);
        GLES20.glUniform1f(uAlpha, 0.30f * k);
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, game.bottleX, surf + 0.012f, 0);
        Matrix.scaleM(model, 0, k, 1f, k);
        Matrix.multiplyMM(tmp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, proj, 0, tmp, 0);
        GLES20.glUniformMatrix4fv(uMVP, 1, false, mvp, 0);
        shadow.draw(aPos, aNormal, aColor);
        GLES20.glUniform1f(uAlpha, 1f);
        GLES20.glDisable(GLES20.GL_BLEND);

        // бутылка
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, game.bottleX, game.bottleY, 0);
        Matrix.rotateM(model, 0,
                (float) Math.toDegrees(game.angle), 0f, 0f, 1f);
        Matrix.multiplyMM(tmp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, proj, 0, tmp, 0);
        GLES20.glUniformMatrix4fv(uMVP, 1, false, mvp, 0);
        bottle.draw(aPos, aNormal, aColor);
    }

    private void drawMesh(Mesh mesh, float x, float y, float z,
                          float scaleX, float rotZ) {
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, x, y, z);
        if (rotZ != 0f) {
            Matrix.rotateM(model, 0, rotZ, 0f, 0f, 1f);
        }
        Matrix.scaleM(model, 0, scaleX, 1f, 1f);
        Matrix.multiplyMM(tmp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, proj, 0, tmp, 0);
        GLES20.glUniformMatrix4fv(uMVP, 1, false, mvp, 0);
        mesh.draw(aPos, aNormal, aColor);
    }

    private static int buildProgram(String vs, String fs) {
        int v = compile(GLES20.GL_VERTEX_SHADER, vs);
        int f = compile(GLES20.GL_FRAGMENT_SHADER, fs);
        int p = GLES20.glCreateProgram();
        GLES20.glAttachShader(p, v);
        GLES20.glAttachShader(p, f);
        GLES20.glLinkProgram(p);
        int[] ok = new int[1];
        GLES20.glGetProgramiv(p, GLES20.GL_LINK_STATUS, ok, 0);
        if (ok[0] == 0) {
            throw new RuntimeException("shader link: "
                    + GLES20.glGetProgramInfoLog(p));
        }
        return p;
    }

    private static int compile(int type, String src) {
        int s = GLES20.glCreateShader(type);
        GLES20.glShaderSource(s, src);
        GLES20.glCompileShader(s);
        int[] ok = new int[1];
        GLES20.glGetShaderiv(s, GLES20.GL_COMPILE_STATUS, ok, 0);
        if (ok[0] == 0) {
            throw new RuntimeException("shader: " + GLES20.glGetShaderInfoLog(s));
        }
        return s;
    }
}
