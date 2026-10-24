'use strict';

// Простое хранилище в памяти с сохранением в JSON-файл.
// Для учебного/демо-проекта этого достаточно; данные переживают перезапуск сервера.

const fs = require('node:fs');
const path = require('node:path');
const { randomUUID, randomBytes } = require('node:crypto');

function addToIndex(index, key, value) {
  let set = index.get(key);
  if (!set) {
    set = new Set();
    index.set(key, set);
  }
  set.add(value);
}

function removeFromIndex(index, key, value) {
  const set = index.get(key);
  if (!set) return;
  set.delete(value);
  if (set.size === 0) index.delete(key);
}

class Store {
  constructor(file = null) {
    this.file = file;
    this.seq = 0;
    this.users = new Map(); // id -> user
    this.usernames = new Map(); // lower-case username -> id
    this.posts = new Map(); // id -> post
    this.postsByAuthor = new Map(); // authorId -> Set(postId)
    this.comments = new Map(); // id -> comment
    this.commentsByPost = new Map(); // postId -> Set(commentId)
    this.sessions = new Map(); // token -> { userId, createdAt }
    this.following = new Map(); // userId -> Set(userId), кого читает пользователь
    this.followers = new Map(); // userId -> Set(userId), кто читает пользователя
    this.likes = new Map(); // postId -> Set(userId)
    this._timer = null;

    if (file && fs.existsSync(file)) {
      this._load(JSON.parse(fs.readFileSync(file, 'utf8')));
    }
  }

  // ---------- persistence ----------

  _load(raw) {
    this.seq = raw.seq || 0;
    for (const u of raw.users || []) this._indexUser(u);
    for (const p of raw.posts || []) this._indexPost(p);
    for (const c of raw.comments || []) this._indexComment(c);
    for (const s of raw.sessions || []) {
      this.sessions.set(s.token, { userId: s.userId, createdAt: s.createdAt });
    }
    for (const [a, b] of raw.follows || []) {
      addToIndex(this.following, a, b);
      addToIndex(this.followers, b, a);
    }
    for (const [postId, userId] of raw.likes || []) addToIndex(this.likes, postId, userId);
  }

  _indexUser(u) {
    this.users.set(u.id, u);
    this.usernames.set(u.username.toLowerCase(), u.id);
  }

  _indexPost(p) {
    this.posts.set(p.id, p);
    addToIndex(this.postsByAuthor, p.authorId, p.id);
  }

  _indexComment(c) {
    this.comments.set(c.id, c);
    addToIndex(this.commentsByPost, c.postId, c.id);
  }

  snapshot() {
    return {
      version: 1,
      seq: this.seq,
      users: [...this.users.values()],
      posts: [...this.posts.values()],
      comments: [...this.comments.values()],
      sessions: [...this.sessions.entries()].map(([token, s]) => ({ token, ...s })),
      follows: [...this.following.entries()].flatMap(([a, set]) => [...set].map((b) => [a, b])),
      likes: [...this.likes.entries()].flatMap(([p, set]) => [...set].map((u) => [p, u])),
    };
  }

  // Отложенная запись, чтобы не писать файл на каждый лайк.
  save() {
    if (!this.file) return;
    clearTimeout(this._timer);
    this._timer = setTimeout(() => {
      try {
        this.flush();
      } catch (err) {
        // Ошибка записи не должна ронять процесс: иначе все запросы получат 502.
        console.error('Не удалось сохранить данные:', err);
      }
    }, 200);
    this._timer.unref();
  }

  flush() {
    if (!this.file) return;
    clearTimeout(this._timer);
    fs.mkdirSync(path.dirname(this.file), { recursive: true });
    const tmp = `${this.file}.tmp`;
    fs.writeFileSync(tmp, JSON.stringify(this.snapshot()));
    fs.renameSync(tmp, this.file);
  }

  // ---------- users ----------

  getUser(id) {
    return this.users.get(id) || null;
  }

  getUserByName(username) {
    const id = this.usernames.get(String(username).toLowerCase());
    return id ? this.users.get(id) : null;
  }

  createUser(fields) {
    const user = {
      id: randomUUID(),
      createdAt: Date.now(),
      isBot: false,
      bio: '',
      avatarColor: '#5b8def',
      passwordHash: null,
      salt: null,
      ...fields,
    };
    this._indexUser(user);
    this.save();
    return user;
  }

  updateUser(id, patch) {
    Object.assign(this.users.get(id), patch);
    this.save();
  }

  listUsers() {
    return [...this.users.values()];
  }

  listBots() {
    return this.listUsers().filter((u) => u.isBot);
  }

  listRealUsers() {
    return this.listUsers().filter((u) => !u.isBot);
  }

  // ---------- sessions ----------

  createSession(userId) {
    const token = randomBytes(32).toString('hex');
    this.sessions.set(token, { userId, createdAt: Date.now() });
    this.save();
    return token;
  }

  getSession(token) {
    return this.sessions.get(token) || null;
  }

  deleteSession(token) {
    this.sessions.delete(token);
    this.save();
  }

  // ---------- follows ----------

  isFollowing(followerId, followingId) {
    return this.following.get(followerId)?.has(followingId) ?? false;
  }

  followersCount(userId) {
    return this.followers.get(userId)?.size ?? 0;
  }

  followingCount(userId) {
    return this.following.get(userId)?.size ?? 0;
  }

  followerIds(userId) {
    return this.followers.get(userId) ?? new Set();
  }

  followingIds(userId) {
    return this.following.get(userId) ?? new Set();
  }

  setFollow(followerId, followingId, on) {
    if (on) {
      addToIndex(this.following, followerId, followingId);
      addToIndex(this.followers, followingId, followerId);
    } else {
      removeFromIndex(this.following, followerId, followingId);
      removeFromIndex(this.followers, followingId, followerId);
    }
    this.save();
  }

  // ---------- posts ----------

  createPost({ authorId, text, createdAt = Date.now() }) {
    const post = { id: randomUUID(), n: ++this.seq, authorId, text, createdAt };
    this._indexPost(post);
    this.save();
    return post;
  }

  deletePost(postId) {
    const post = this.posts.get(postId);
    if (!post) return;
    this.posts.delete(postId);
    removeFromIndex(this.postsByAuthor, post.authorId, postId);
    for (const commentId of this.commentsByPost.get(postId) ?? []) this.comments.delete(commentId);
    this.commentsByPost.delete(postId);
    this.likes.delete(postId);
    this.save();
  }

  postCount(authorId) {
    return this.postsByAuthor.get(authorId)?.size ?? 0;
  }

  // Посты, отсортированные от новых к старым.
  sortedPosts(filter = () => true) {
    return [...this.posts.values()]
      .filter(filter)
      .sort((a, b) => b.createdAt - a.createdAt || b.n - a.n);
  }

  recentPosts(since) {
    return [...this.posts.values()].filter((p) => p.createdAt >= since);
  }

  // ---------- likes ----------

  hasLiked(postId, userId) {
    return this.likes.get(postId)?.has(userId) ?? false;
  }

  likeCount(postId) {
    return this.likes.get(postId)?.size ?? 0;
  }

  setLike(postId, userId, on) {
    if (on) addToIndex(this.likes, postId, userId);
    else removeFromIndex(this.likes, postId, userId);
    this.save();
  }

  // ---------- comments ----------

  createComment({ postId, authorId, text }) {
    const comment = { id: randomUUID(), n: ++this.seq, postId, authorId, text, createdAt: Date.now() };
    this._indexComment(comment);
    this.save();
    return comment;
  }

  commentCount(postId) {
    return this.commentsByPost.get(postId)?.size ?? 0;
  }

  listComments(postId) {
    return [...(this.commentsByPost.get(postId) ?? [])]
      .map((id) => this.comments.get(id))
      .sort((a, b) => a.createdAt - b.createdAt || a.n - b.n);
  }
}

module.exports = { Store };
