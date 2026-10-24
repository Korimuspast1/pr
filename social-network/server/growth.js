'use strict';

// Виртуальная «жизнь» соцсети: боты-аккаунты публикуют посты, ставят лайки
// и подписываются на пользователей. Рост подписчиков у реальных аккаунтов
// ограничен «лимитом», который растёт со временем, а также от постов и лайков.
// Поэтому подписчики прибывают постепенно, а не все сразу.

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;

const BOT_PROFILES = [
  { username: 'anna_sun', displayName: 'Анна', bio: 'Кофе, книги и утренние прогулки ☕', avatarColor: '#e07a5f' },
  { username: 'max_road', displayName: 'Максим', bio: 'Фотографирую города. Люблю поезда 🚆', avatarColor: '#3d405b' },
  { username: 'lena_cook', displayName: 'Лена', bio: 'Готовлю всё подряд, рецепты в ленте 🍝', avatarColor: '#81b29a' },
  { username: 'ivan_code', displayName: 'Иван', bio: 'Пишу код и иногда сплю 💻', avatarColor: '#f2cc8f' },
  { username: 'mila_art', displayName: 'Мила', bio: 'Рисую акварелью и говорю о цвете 🎨', avatarColor: '#9d4edd' },
  { username: 'dan_sport', displayName: 'Данил', bio: 'Бег по утрам, зал по вечерам 🏃', avatarColor: '#2a9d8f' },
  { username: 'olga_plants', displayName: 'Ольга', bio: 'Мои растения важнее мебели 🌿', avatarColor: '#6a994e' },
  { username: 'sasha_music', displayName: 'Саша', bio: 'Слушаю всё, играю на гитаре 🎸', avatarColor: '#bc4749' },
  { username: 'kate_travel', displayName: 'Катя', bio: 'Живу в дороге уже три года ✈️', avatarColor: '#118ab2' },
  { username: 'artem_films', displayName: 'Артём', bio: 'Смотрю кино и пишу короткие рецензии 🎬', avatarColor: '#577590' },
  { username: 'nina_books', displayName: 'Нина', bio: 'Читаю по книге в неделю 📚', avatarColor: '#b56576' },
  { username: 'pavel_bike', displayName: 'Павел', bio: 'Велосипед — лучший транспорт 🚲', avatarColor: '#ffb703' },
  { username: 'yana_design', displayName: 'Яна', bio: 'Дизайн, шрифты и пустой кофе ✏️', avatarColor: '#fb8500' },
  { username: 'roma_space', displayName: 'Рома', bio: 'Звёзды, телескопы и космос 🔭', avatarColor: '#264653' },
  { username: 'vika_yoga', displayName: 'Вика', bio: 'Йога, дыхание и тишина 🧘', avatarColor: '#e76f51' },
  { username: 'tim_games', displayName: 'Тимур', bio: 'Инди-игры и ностальгия по 2008 году 🎮', avatarColor: '#4361ee' },
  { username: 'sofia_pets', displayName: 'София', bio: 'Два кота и один ноутбук 🐱', avatarColor: '#f72585' },
  { username: 'egor_moto', displayName: 'Егор', bio: 'Мотоцикл, гаражи и ремонт 🏍️', avatarColor: '#07beb8' },
  { username: 'alina_photo', displayName: 'Алина', bio: 'Свет важнее камеры 📷', avatarColor: '#8338ec' },
  { username: 'kirill_tech', displayName: 'Кирилл', bio: 'Новости технологий без хайпа 🧠', avatarColor: '#3a0ca3' },
  { username: 'polina_food', displayName: 'Полина', bio: 'Ем и рассказываю 🍣', avatarColor: '#ff006e' },
  { username: 'denis_city', displayName: 'Денис', bio: 'Архитектура и районы, которые мы не замечаем 🏙️', avatarColor: '#1b4332' },
  { username: 'ksenia_fit', displayName: 'Ксения', bio: 'Здоровый завтрак — это не скучно 🥑', avatarColor: '#52b788' },
  { username: 'oleg_chess', displayName: 'Олег', bio: 'Шахматы каждый день ♟️', avatarColor: '#495057' },
  { username: 'zhenya_fish', displayName: 'Женя', bio: 'Рыбалка, озёра и тишина 🎣', avatarColor: '#0077b6' },
  { username: 'tanya_diary', displayName: 'Таня', bio: 'Личный дневник, открытый для всех 📖', avatarColor: '#d62828' },
  { username: 'boris_history', displayName: 'Борис', bio: 'Истории, которых не было в школе 🏛️', avatarColor: '#6d6875' },
  { username: 'alex_startup', displayName: 'Алекс', bio: 'Строю маленькие продукты и учусь на ошибках 🚀', avatarColor: '#386641' },
  { username: 'masha_vinyl', displayName: 'Маша', bio: 'Винил, шум дождя и старые плёнки 💿', avatarColor: '#9b2226' },
  { username: 'igor_garden', displayName: 'Игорь', bio: 'Огород на балконе — это серьёзно 🥕', avatarColor: '#588157' },
  { username: 'ruslan_run', displayName: 'Руслан', bio: 'Марафоны и кроссовки, которые жалко носить 👟', avatarColor: '#0096c7' },
];

const POST_TEXTS = [
  'Доброе утро! Первый кофе уже в руке, день обещает быть хорошим ☕',
  'Сегодня наконец-то дописал то, что откладывал две недели. Чувствую себя счастливым.',
  'Кто знает хороший способ не забывать пить воду во время работы? Пробую таймер.',
  'Гуляли вечером по набережной. Закат был такой, что даже телефон не хотелось доставать 🌅',
  'Новый рецепт: паста с лимоном и шпинатом. Быстро, вкусно, почти без посуды 🍝',
  'Если бы я мог выбрать суперсилу, это была бы способность всегда находить свободную розетку.',
  'Прочитал книгу за выходные. Рекомендую всем, кто любит медленные истории.',
  'Растение на подоконнике наконец выпустило новый лист! Маленькая, но победа 🌱',
  'Сегодня проверил старые фотографии. Это было лето, которого мне очень не хватает.',
  'Какие у вас любимые сериалы этой осенью? Нужно что-то, чтобы смотреть под плед.',
  'Попробовал бегать по утрам три недели подряд. Оказалось, что я почти полюбил это 🏃',
  'Маленький вопрос: вы читаете бумажные книги или всё в телефоне?',
  'Обновил свой плейлист для поездок. Если любите инди-рок, зацените 🎧',
  'Скучный день, но в нём есть одна хорошая новость: ужин будет вкусным.',
  'Сделал скриншот звёздного неба. Вышло не очень, зато атмосферно ✨',
  'Заметил, что самые полезные разговоры случаются в очереди в кофейне.',
  'Небольшой совет: пишите мысли на бумаге до того, как откроете ленту.',
  'Сегодня видел, как уличный музыкант играл для одного слушателя. Это было красиво.',
  'Сплю слишком мало, зато много думаю. Баланс пока не найден 😅',
  'Дождь за окном, чай, плед. Вот и вся моя программа на вечер 🌧️',
  'Кто-нибудь был в новом музее в центре? Стоит ли идти в будний день?',
  'Выбрал новую ручку для блокнота, и почерк внезапно стал красивее ✒️',
  'Нашёл в шкафу старую кассету. Интересно, что на ней записано.',
  'Пару часов провёл без телефона — и понял, что мир никуда не делся 📵',
  'Вечерняя прогулка с собакой по парку. Она счастлива, я тоже.',
  'Приготовил домашний хлеб впервые. Выглядит неровно, но пахнет очень хорошо 🍞',
  'Главное правило на этой неделе: меньше планов, больше времени на простые вещи.',
  'Посмотрел фильм, который все советовали. Ожидания оправдались.',
  'Сегодня научился завязывать новый узел. Пригодится в походе, надеюсь 🪢',
  'Снова дождь, но в кафе уютно. Работаю и слушаю разговоры вокруг.',
  'Может, кто-то знает хорошую книгу о самоорганизации без воды? Спасибо заранее!',
  'Составил список мест, которые хочу увидеть до конца года. Первое место — море 🌊',
];

const BOT_POST_CHANCE = 0.01; // шанс поста у бота за один тик
const BOT_LIKE_CHANCE_FOLLOWED = 0.15; // шанс лайка от бота, который читает автора
const BOT_LIKE_CHANCE_OTHER = 0.02; // шанс лайка от остальных ботов
const FOLLOWER_GAIN_CHANCE = 0.5; // шанс, что за тик появится новый подписчик (если есть место в лимите)
const MAX_BOT_FOLLOWERS = 500;

function pick(list, rng) {
  return list[Math.floor(rng() * list.length)];
}

function shuffle(list, rng) {
  const copy = [...list];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

// Сколько подписчиков аккаунт «заслужил» на данный момент.
function followerCap(user, { now, postCount, likesReceived }) {
  const ageMinutes = Math.max(0, (now - user.createdAt) / 60000);
  const raw = 2 + ageMinutes * 1 + postCount * 3 + likesReceived * 1.5;
  return Math.min(MAX_BOT_FOLLOWERS, Math.floor(raw));
}

// Создаёт ботов и стартовые посты, если база пуста. Возвращает число созданных ботов.
function seedBots(store, { now = Date.now(), rng = Math.random } = {}) {
  if (store.listBots().length > 0) return 0;

  const bots = BOT_PROFILES.map((profile) =>
    store.createUser({
      ...profile,
      isBot: true,
      createdAt: now - Math.floor(rng() * 30 * DAY),
    }),
  );

  // Боты читают друг друга, образуя «сеть».
  for (const bot of bots) {
    const others = shuffle(bots.filter((b) => b.id !== bot.id), rng);
    const count = 4 + Math.floor(rng() * 5);
    for (const target of others.slice(0, count)) store.setFollow(bot.id, target.id, true);
  }

  // Стартовые посты разбросаны за последние 3 дня.
  for (const bot of bots) {
    const count = 2 + Math.floor(rng() * 3);
    for (let i = 0; i < count; i++) {
      store.createPost({
        authorId: bot.id,
        text: pick(POST_TEXTS, rng),
        createdAt: now - Math.floor(rng() * 3 * DAY),
      });
    }
  }
  return bots.length;
}

// Подписывает нового пользователя на несколько случайных ботов, чтобы лента не была пустой.
function followStarterBots(store, userId, { count = 3, rng = Math.random } = {}) {
  const bots = store.listBots();
  for (const bot of shuffle(bots, rng).slice(0, count)) {
    store.setFollow(userId, bot.id, true);
  }
}

// Один шаг симуляции. Все параметры передаются явно, поэтому функцию легко тестировать.
function growthTick(store, { now = Date.now(), rng = Math.random } = {}) {
  const stats = { botPosts: 0, botLikes: 0, newFollowers: 0 };
  const bots = store.listBots();
  if (bots.length === 0) return stats;

  // 1. Боты иногда публикуют посты.
  for (const bot of bots) {
    if (rng() < BOT_POST_CHANCE) {
      store.createPost({ authorId: bot.id, text: pick(POST_TEXTS, rng), createdAt: now });
      stats.botPosts++;
    }
  }

  // 2. Боты ставят лайки свежим постам (чаще тем авторам, на которых подписаны).
  for (const post of store.recentPosts(now - DAY)) {
    const followers = store.followerIds(post.authorId);
    for (const bot of bots) {
      if (bot.id === post.authorId || store.hasLiked(post.id, bot.id)) continue;
      const chance = followers.has(bot.id) ? BOT_LIKE_CHANCE_FOLLOWED : BOT_LIKE_CHANCE_OTHER;
      if (rng() < chance) {
        store.setLike(post.id, bot.id, true);
        stats.botLikes++;
      }
    }
  }

  // 3. Реальные пользователи получают подписчиков постепенно, в пределах лимита.
  const likesByAuthor = new Map();
  for (const [postId, likers] of store.likes) {
    const post = store.posts.get(postId);
    if (!post) continue;
    likesByAuthor.set(post.authorId, (likesByAuthor.get(post.authorId) ?? 0) + likers.size);
  }

  for (const user of store.listRealUsers()) {
    const cap = followerCap(user, {
      now,
      postCount: store.postCount(user.id),
      likesReceived: likesByAuthor.get(user.id) ?? 0,
    });
    if (store.followersCount(user.id) >= cap) continue;
    if (rng() >= FOLLOWER_GAIN_CHANCE) continue;

    const candidates = bots.filter((b) => !store.isFollowing(b.id, user.id));
    if (candidates.length === 0) continue;
    store.setFollow(pick(candidates, rng).id, user.id, true);
    stats.newFollowers++;
  }

  return stats;
}

// Запускает симуляцию в фоне.
function startGrowth(store, { intervalMs = 10000, rng = Math.random, onError = console.error } = {}) {
  const timer = setInterval(() => {
    try {
      growthTick(store, { now: Date.now(), rng });
    } catch (err) {
      onError(err);
    }
  }, intervalMs);
  timer.unref();
  return timer;
}

module.exports = {
  BOT_PROFILES,
  POST_TEXTS,
  seedBots,
  followStarterBots,
  growthTick,
  followerCap,
  startGrowth,
};
