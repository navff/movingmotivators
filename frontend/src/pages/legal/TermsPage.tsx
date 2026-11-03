import source from '@legal/offer.md?raw'
import { Page } from '@/components/Layout'
import { Prose } from '@/components/Markdown'

export default function TermsPage() {
  return (
    <Page>
      <Prose source={source} />
    </Page>
  )
}
