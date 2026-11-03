import { useEffect } from 'react'
import { useLocation } from 'react-router'
import raw from '@legal/consent.md?raw'
import { Page } from '@/components/Layout'
import { Prose } from '@/components/Markdown'

// служебный абзац про хранение версий — для разработчиков, не для пользователей
const source = raw.replace(/^Версии согласий хранятся.*$/m, '')

export default function ConsentPage() {
  const { hash } = useLocation()
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView()
  }, [hash])
  return (
    <Page>
      <Prose source={source} anchors={{ 'Организатор': 'organizer', 'Участник неанонимного': 'participant', 'Участник анонимного': 'anonymous' }} />
    </Page>
  )
}
