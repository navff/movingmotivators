import type { ReactNode } from 'react'
import {
  DndContext, KeyboardSensor, PointerSensor, TouchSensor, closestCenter, useSensor, useSensors, type DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext, arrayMove, sortableKeyboardCoordinates, useSortable,
  horizontalListSortingStrategy, verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'

/** Сортируемый список на dnd-kit: мышь, тач (с задержкой, чтобы не мешать скроллу) и клавиатура. */
export function SortableList<T extends string>({
  items,
  onChange,
  direction = 'vertical',
  className,
  renderItem,
  disabled,
}: {
  items: T[]
  onChange: (items: T[]) => void
  direction?: 'vertical' | 'horizontal'
  className?: string
  disabled?: boolean
  renderItem: (id: T, index: number, handle: DragHandleProps) => ReactNode
}) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const onDragEnd = (e: DragEndEvent) => {
    const { active, over } = e
    if (!over || active.id === over.id) return
    onChange(arrayMove(items, items.indexOf(active.id as T), items.indexOf(over.id as T)))
  }
  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <SortableContext items={items} strategy={direction === 'vertical' ? verticalListSortingStrategy : horizontalListSortingStrategy} disabled={disabled}>
        <div className={className}>
          {items.map((id, i) => (
            <SortableItem key={id} id={id}>
              {(handle) => renderItem(id, i, handle)}
            </SortableItem>
          ))}
        </div>
      </SortableContext>
    </DndContext>
  )
}

export type DragHandleProps = Record<string, unknown> & { isDragging: boolean }

function SortableItem({ id, children }: { id: string; children: (handle: DragHandleProps) => ReactNode }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id })
  return (
    <div
      ref={setNodeRef}
      style={{
        transform: CSS.Transform.toString(transform),
        transition,
        zIndex: isDragging ? 10 : undefined,
        position: 'relative',
        touchAction: 'manipulation',
      }}
    >
      {children({ ...attributes, ...listeners, isDragging })}
    </div>
  )
}

export function move<T>(items: T[], index: number, delta: number): T[] {
  const to = index + delta
  if (to < 0 || to >= items.length) return items
  return arrayMove(items, index, to)
}
