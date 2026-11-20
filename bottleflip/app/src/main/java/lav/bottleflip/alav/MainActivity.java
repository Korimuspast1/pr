package lav.bottleflip.alav;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.widget.TextView;

/** Игровой экран: GLSurfaceView + HUD (счёт, рекорд, экран проигрыша). */
public class MainActivity extends Activity {

    private Game game;
    private GLSurfaceView glView;
    private TextView scoreView;
    private TextView bestView;
    private View gameOverPanel;
    private View hintView;
    private TextView finalScore;
    private TextView finalBest;
    private SoundFx sound;
    private boolean firstLandingDone;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_game);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        hideSystemUi();

        scoreView = findViewById(R.id.hud_score);
        bestView = findViewById(R.id.hud_best);
        gameOverPanel = findViewById(R.id.game_over_panel);
        hintView = findViewById(R.id.hud_hint);
        finalScore = findViewById(R.id.final_score);
        finalBest = findViewById(R.id.final_best);

        SharedPreferences prefs = getPreferences(Context.MODE_PRIVATE);

        sound = new SoundFx();
        sound.init();

        game = new Game();
        game.best = prefs.getInt("best", 0);
        game.reset(System.nanoTime());
        game.listener = event -> {
            if (event == Game.EV_JUMP) {
                sound.playJump();
            } else if (event == Game.EV_LAND) {
                sound.playLand();
                firstLandingDone = true;
                runOnUiThread(() -> {
                    scoreView.setText(String.valueOf(game.score));
                    hintView.setVisibility(View.GONE);
                });
            } else if (event == Game.EV_FAIL) {
                sound.playFail();
                if (game.score > game.best) {
                    game.best = game.score;
                    prefs.edit().putInt("best", game.best).apply();
                }
                runOnUiThread(() -> {
                    finalScore.setText(getString(R.string.score_fmt, game.score));
                    finalBest.setText(getString(R.string.best_fmt, game.best));
                    gameOverPanel.setVisibility(View.VISIBLE);
                });
            }
        };

        bestView.setText(getString(R.string.best_fmt, game.best));

        glView = findViewById(R.id.gl_view);
        glView.setEGLContextClientVersion(2);
        glView.setEGLConfigChooser(8, 8, 8, 8, 16, 0);
        glView.setRenderer(new GameRenderer(game));
        glView.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        if (event.getAction() == MotionEvent.ACTION_DOWN) {
            if (game.state == Game.OVER) {
                restart();
            } else {
                glView.queueEvent(() -> game.tap());
            }
            return true;
        }
        return super.onTouchEvent(event);
    }

    private void restart() {
        glView.queueEvent(() -> game.reset(System.nanoTime()));
        scoreView.setText("0");
        bestView.setText(getString(R.string.best_fmt, game.best));
        gameOverPanel.setVisibility(View.GONE);
        if (!firstLandingDone) {
            hintView.setVisibility(View.VISIBLE);
        }
    }

    private void hideSystemUi() {
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                        | View.SYSTEM_UI_FLAG_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
    }

    @Override
    protected void onResume() {
        super.onResume();
        hideSystemUi();
        glView.onResume();
    }

    @Override
    protected void onPause() {
        glView.onPause();
        super.onPause();
    }
}
