/**
 * Мягкие переносы (U+00AD) в длинных русских словах по упрощённым слоговым правилам.
 * Нужны потому, что `hyphens: auto` для русского работает не везде (Chrome на Windows
 * без словаря просто рвёт слово в любом месте и без дефиса). Мягкий перенос невидим,
 * пока слово помещается в строку.
 */
const SHY = '­'
const VOWELS = 'аеёиоуыэюя'
const SIGNS = 'йьъ'
const MIN_WORD = 7
const MIN_LEFT = 2
const MIN_RIGHT = 3

const isVowel = (c: string) => VOWELS.includes(c)
const isLetter = (c: string) => /[а-яё]/.test(c)

export function hyphenateWord(word: string): string {
  if (word.length < MIN_WORD) return word
  const w = word.toLowerCase()
  if (![...w].every(isLetter)) return word

  const breaks: number[] = []
  for (let i = MIN_LEFT; i <= w.length - MIN_RIGHT; i++) {
    const left = w.slice(0, i)
    const right = w.slice(i)
    // в каждой части должен остаться слог
    if (![...left].some(isVowel) || ![...right].some(isVowel)) continue
    // й, ь, ъ не начинают строку
    if (SIGNS.includes(w[i])) continue
    const prev = w[i - 1]
    const cur = w[i]
    const next = w[i + 1] ?? ''
    const beforePrev = w[i - 2] ?? ''
    const ok =
      SIGNS.includes(prev) || // после й/ь/ъ: «боль-шой», «объ-яв-ле-ние»
      (isVowel(prev) && !isVowel(cur) && isVowel(next)) || // V|CV: «по-ни-мать»
      // в стечении согласных — только после первой: «цен-ности», «ис-сле-до-вать», «пыт-ство»
      (isVowel(beforePrev) && !isVowel(prev) && !isVowel(cur))
    if (ok) breaks.push(i)
  }

  let out = ''
  let from = 0
  for (const b of breaks) {
    out += word.slice(from, b) + SHY
    from = b
  }
  return out + word.slice(from)
}

/** Расставляет мягкие переносы во всех словах текста, не трогая пробелы и знаки препинания. */
export function hyphenate(text: string): string {
  return text.replace(/[А-Яа-яЁё]+/g, hyphenateWord)
}
