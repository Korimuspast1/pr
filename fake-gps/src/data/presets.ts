export interface PresetCity {
  name: string;
  country: string;
  lat: number;
  lng: number;
}

export const PRESET_CITIES: PresetCity[] = [
  { name: 'Варшава (Центр)', country: 'Польша', lat: 52.2297, lng: 21.0122 },
  { name: 'Краков (Рынок)', country: 'Польша', lat: 50.0619, lng: 19.9369 },
  { name: 'Гданьск (Старый город)', country: 'Польша', lat: 54.352, lng: 18.6466 },
  { name: 'Вроцлав (Площадь Рынок)', country: 'Польша', lat: 51.1079, lng: 17.0385 },
  { name: 'Познань', country: 'Польша', lat: 52.4064, lng: 16.9252 },
  { name: 'Лодзь', country: 'Польша', lat: 51.7592, lng: 19.456 },
  { name: 'Закопане (Татры)', country: 'Польша', lat: 49.2992, lng: 19.9496 },
  { name: 'Москва (Красная площадь)', country: 'Россия', lat: 55.7539, lng: 37.6208 },
  { name: 'Санкт-Петербург', country: 'Россия', lat: 59.9343, lng: 30.3351 },
  { name: 'Минск', country: 'Беларусь', lat: 53.9006, lng: 27.559 },
  { name: 'Киев', country: 'Украина', lat: 50.4501, lng: 30.5234 },
  { name: 'Алматы', country: 'Казахстан', lat: 43.2389, lng: 76.8897 },
  { name: 'Тбилиси', country: 'Грузия', lat: 41.7151, lng: 44.8271 },
  { name: 'Стамбул', country: 'Турция', lat: 41.0082, lng: 28.9784 },
  { name: 'Берлин (Александерплац)', country: 'Германия', lat: 52.5219, lng: 13.4132 },
  { name: 'Париж (Эйфелева башня)', country: 'Франция', lat: 48.8584, lng: 2.2945 },
  { name: 'Лондон (Биг-Бен)', country: 'Великобритания', lat: 51.5007, lng: -0.1246 },
  { name: 'Прага', country: 'Чехия', lat: 50.0755, lng: 14.4378 },
  { name: 'Рим (Колизей)', country: 'Италия', lat: 41.8902, lng: 12.4922 },
  { name: 'Дубай (Бурдж-Халифа)', country: 'ОАЭ', lat: 25.1972, lng: 55.2744 },
  { name: 'Нью-Йорк (Таймс-сквер)', country: 'США', lat: 40.758, lng: -73.9855 },
  { name: 'Лос-Анджелес', country: 'США', lat: 34.0522, lng: -118.2437 },
  { name: 'Токио (Сибуя)', country: 'Япония', lat: 35.6595, lng: 139.7004 },
  { name: 'Сеул', country: 'Южная Корея', lat: 37.5665, lng: 126.978 },
  { name: 'Сингапур', country: 'Сингапур', lat: 1.2834, lng: 103.8607 },
  { name: 'Сидней (Опера)', country: 'Австралия', lat: -33.8568, lng: 151.2153 },
];
