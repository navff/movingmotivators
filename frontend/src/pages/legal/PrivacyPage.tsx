import source from '@legal/privacy.md?raw'
import { Page } from '@/components/Layout'
import { Prose } from '@/components/Markdown'

export default function PrivacyPage() {
  return (
    <Page>
      <Prose source={source} />
    </Page>
  )
}
