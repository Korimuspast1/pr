package lav.visaroom.alav;

import android.app.Activity;
import android.os.Bundle;

public class ChatActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_chat);
        findViewById(R.id.back).setOnClickListener(v -> finish());
    }
}
