package lav.visaroom.alav;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.View;
import android.widget.EditText;
import android.widget.TextView;

import java.util.Locale;

public class ChatsActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_chats);

        TextView accountEmail = findViewById(R.id.account_email);
        String email = TokenStore.email(this);
        accountEmail.setText(email == null ? "" : email);

        View officialChat = findViewById(R.id.official_chat);
        TextView noChats = findViewById(R.id.no_chats);
        EditText search = findViewById(R.id.search);

        officialChat.setOnClickListener(v ->
                startActivity(new Intent(this, ChatActivity.class)));

        findViewById(R.id.sign_out).setOnClickListener(v -> {
            TokenStore.clear(this);
            Intent intent = new Intent(this, AuthActivity.class);
            intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
            startActivity(intent);
            finish();
        });

        search.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                String query = s.toString().trim().toLowerCase(Locale.ROOT);
                boolean match = query.length() == 0
                        || "visaroom".contains(query)
                        || "official channel".contains(query);
                officialChat.setVisibility(match ? View.VISIBLE : View.GONE);
                noChats.setVisibility(match ? View.GONE : View.VISIBLE);
            }

            @Override
            public void afterTextChanged(Editable s) {
            }
        });
    }
}
