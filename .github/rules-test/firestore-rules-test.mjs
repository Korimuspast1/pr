import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import {
  assertFails,
  assertSucceeds,
  initializeTestEnvironment,
} from "@firebase/rules-unit-testing";
import {
  Bytes,
  doc,
  getDoc,
  setDoc,
  updateDoc,
} from "firebase/firestore";

const ALICE = "aliceUid111111111111111111";
const BOB = "bobUid2222222222222222222";
const CAROL = "carolUid33333333333333333";
const ADMIN = "adminUid99999999999999999";
const ADMIN_EMAIL = "debugger821@gmail.com";

const emulatorHost = process.env.FIRESTORE_EMULATOR_HOST || "127.0.0.1:8080";
const [host, portText] = emulatorHost.split(":");
const rules = readFileSync(
  new URL("../../firebase/firestore.rules", import.meta.url),
  "utf8",
);

const testEnv = await initializeTestEnvironment({
  projectId: "demo-visaroom",
  firestore: {
    rules,
    host,
    port: Number(portText || "8080"),
  },
});

function dbFor(uid, email) {
  return testEnv
    .authenticatedContext(uid, {
      email,
      email_verified: true,
      firebase: {
        sign_in_provider: "password",
        identities: { email: [email] },
      },
    })
    .firestore();
}

function privateProfile(username, publicId, overrides = {}) {
  return {
    username,
    bio: `${username} bio`,
    hasAvatar: false,
    avatarUpdatedAt: 0,
    discoverable: true,
    publicId,
    showBio: true,
    showAvatar: true,
    showId: true,
    blocked: false,
    blockedAt: 0,
    verified: false,
    ...overrides,
  };
}

function publicProfile(username, publicId, overrides = {}) {
  const result = {
    username,
    bio: `${username} bio`,
    hasAvatar: false,
    avatarUpdatedAt: 0,
    showBio: true,
    showAvatar: true,
    showId: true,
    blocked: false,
    verified: false,
    ...overrides,
  };
  if (publicId > 0 && result.showId) {
    result.publicId = publicId;
  }
  return result;
}

async function seed() {
  await testEnv.withSecurityRulesDisabled(async (context) => {
    const db = context.firestore();
    await setDoc(doc(db, "users", ALICE), privateProfile("alice", 100001));
    await setDoc(doc(db, "users", BOB), privateProfile("bob", 100002));
    await setDoc(
      doc(db, "users", CAROL),
      privateProfile("carol", 100003, { showId: false }),
    );
    await setDoc(doc(db, "profiles", ALICE), publicProfile("alice", 100001));
    await setDoc(doc(db, "profiles", BOB), publicProfile("bob", 100002));
    await setDoc(
      doc(db, "profiles", CAROL),
      publicProfile("carol", 100003, { showId: false }),
    );
    await setDoc(doc(db, "publicIds", "100001"), { uid: ALICE });
    await setDoc(doc(db, "publicIds", "100002"), { uid: BOB });
    await setDoc(doc(db, "publicIds", "100003"), { uid: CAROL });
  });
}

function canonicalChatId(a, b) {
  const [first, second] = [a, b].sort();
  return (
    "vrp1_" +
    createHash("sha256")
      .update(`VRProto/1/direct\n${first}\n${second}`, "utf8")
      .digest("hex")
  );
}

try {
  await seed();

  const aliceDb = dbFor(ALICE, "alice@example.com");
  const bobDb = dbFor(BOB, "bob@example.com");
  const carolDb = dbFor(CAROL, "carol@example.com");
  const adminDb = dbFor(ADMIN, ADMIN_EMAIL);
  const anonDb = testEnv.unauthenticatedContext().firestore();

  await assertSucceeds(getDoc(doc(aliceDb, "users", ALICE)));
  await assertFails(getDoc(doc(bobDb, "users", ALICE)));
  await assertFails(getDoc(doc(anonDb, "profiles", ALICE)));

  const verifier = `120000:${"A".repeat(22)}==:${"B".repeat(43)}=`;
  const twoFaRef = doc(aliceDb, "users", ALICE, "security", "twoFa");
  await assertSucceeds(setDoc(twoFaRef, { enabled: true, verifier }));
  await assertSucceeds(
    getDoc(doc(aliceDb, "users", ALICE, "security", "twoFa")),
  );
  await assertFails(getDoc(doc(bobDb, "users", ALICE, "security", "twoFa")));
  await assertFails(
    getDoc(doc(adminDb, "users", ALICE, "security", "twoFa")),
  );

  await assertFails(
    setDoc(
      doc(aliceDb, "profiles", ALICE),
      publicProfile("alice", 100001, { bio: "not the private bio" }),
    ),
  );

  await assertFails(getDoc(doc(aliceDb, "publicIds", "100003")));
  await assertSucceeds(getDoc(doc(carolDb, "publicIds", "100003")));
  await assertSucceeds(getDoc(doc(bobDb, "publicIds", "100001")));

  await assertSucceeds(
    setDoc(doc(aliceDb, "usernames", "alice"), {
      uid: ALICE,
      username: "alice",
      usernameLower: "alice",
      publicId: 100001,
      verified: false,
    }),
  );
  await assertFails(
    updateDoc(doc(bobDb, "usernames", "alice"), { uid: BOB }),
  );

  const now = Date.now();
  const chatId = canonicalChatId(ALICE, BOB);
  const chat = {
    participants: [ALICE, BOB],
    type: "direct",
    protocol: "VRProto/1",
    createdAt: now,
    updatedAt: now,
    lastMessageText: "",
    lastMessageAt: 0,
    lastSenderUid: "",
    lastMessageId: "",
  };
  await assertSucceeds(setDoc(doc(aliceDb, "chats", chatId), chat));
  await assertFails(
    setDoc(doc(aliceDb, "chats", `vrp1_${"0".repeat(64)}`), chat),
  );
  await assertSucceeds(getDoc(doc(bobDb, "chats", chatId)));
  await assertFails(getDoc(doc(carolDb, "chats", chatId)));

  const messageId = "mtest0123456789abcdef0123";
  const message = {
    protocolVersion: 1,
    type: "text",
    senderUid: ALICE,
    text: "hello",
    createdAt: now + 1,
    clientId: messageId,
  };
  await assertSucceeds(
    setDoc(doc(aliceDb, "chats", chatId, "messages", messageId), message),
  );
  await assertFails(
    setDoc(doc(bobDb, "chats", chatId, "messages", "mtest1111111111111111111"), {
      ...message,
      clientId: "mtest1111111111111111111",
    }),
  );
  await assertFails(
    setDoc(
      doc(carolDb, "chats", chatId, "messages", "mtest2222222222222222222"),
      {
        ...message,
        senderUid: CAROL,
        clientId: "mtest2222222222222222222",
      },
    ),
  );

  const aliceIndexRef = doc(aliceDb, "users", ALICE, "chats", chatId);
  await assertSucceeds(
    setDoc(aliceIndexRef, {
      chatId,
      peerUid: BOB,
      updatedAt: now,
      lastMessageText: "",
      lastMessageAt: 0,
      lastSenderUid: "",
      lastMessageId: "",
      lastReadAt: 0,
    }),
  );
  await assertSucceeds(
    updateDoc(aliceIndexRef, {
      updatedAt: message.createdAt,
      lastMessageText: message.text,
      lastMessageAt: message.createdAt,
      lastSenderUid: ALICE,
      lastMessageId: messageId,
    }),
  );
  await assertFails(
    updateDoc(doc(bobDb, "users", ALICE, "chats", chatId), {
      lastReadAt: message.createdAt,
    }),
  );

  const fileId = "mfile0123456789abcdef0123";
  const fileCreatedAt = now + 2;
  const fileSha256 =
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
  const fileRef = doc(aliceDb, "chats", chatId, "files", fileId);
  await assertSucceeds(
    setDoc(fileRef, {
      protocolVersion: 1,
      senderUid: ALICE,
      recipientUid: BOB,
      fileName: "a.txt",
      mimeType: "text/plain",
      fileSize: 3,
      chunkSize: 614400,
      chunkCount: 1,
      fileSha256,
      createdAt: fileCreatedAt,
      clientId: fileId,
      downloadLimit: 2,
      downloadCount: 0,
      senderDownloaded: false,
      recipientDownloaded: false,
    }),
  );
  const chunkRef = doc(aliceDb, "chats", chatId, "files", fileId, "chunks", "0");
  await assertSucceeds(
    setDoc(chunkRef, {
      index: 0,
      data: Bytes.fromUint8Array(new Uint8Array([97, 98, 99])),
    }),
  );
  await assertSucceeds(
    setDoc(doc(aliceDb, "chats", chatId, "messages", fileId), {
      protocolVersion: 1,
      type: "file",
      senderUid: ALICE,
      text: "a.txt",
      createdAt: fileCreatedAt,
      clientId: fileId,
      fileId,
      fileName: "a.txt",
      mimeType: "text/plain",
      fileSize: 3,
      chunkCount: 1,
      chunkSize: 614400,
      fileSha256,
    }),
  );
  await assertSucceeds(
    getDoc(doc(bobDb, "chats", chatId, "files", fileId, "chunks", "0")),
  );
  await assertFails(
    getDoc(doc(carolDb, "chats", chatId, "files", fileId, "chunks", "0")),
  );
  await assertSucceeds(
    updateDoc(doc(bobDb, "chats", chatId, "files", fileId), {
      downloadCount: 1,
      recipientDownloaded: true,
    }),
  );
  await assertFails(
    getDoc(doc(bobDb, "chats", chatId, "files", fileId, "chunks", "0")),
  );

  const announcementId = `a${now}_0123456789abcdef`;
  const announcement = {
    text: "official",
    authorUid: ALICE,
    createdAt: new Date(now),
  };
  await assertFails(
    setDoc(doc(aliceDb, "announcements", announcementId), announcement),
  );
  await assertSucceeds(
    setDoc(doc(adminDb, "announcements", announcementId), {
      ...announcement,
      authorUid: ADMIN,
    }),
  );

  console.log("Firestore rules smoke tests passed");
} finally {
  await testEnv.cleanup();
}
