<script setup lang="ts">
import { AlertTriangle } from 'lucide-vue-next';
import {
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogOverlay,
  AlertDialogPortal,
  AlertDialogRoot,
  AlertDialogTitle,
} from 'reka-ui';

const props = defineProps<{
  open: boolean;
  certificateName: string;
  busy: boolean;
}>();

const emit = defineEmits<{
  cancel: [];
  confirm: [];
}>();

function handleOpenChange(open: boolean): void {
  if (!open && !props.busy) {
    emit('cancel');
  }
}
</script>

<template>
  <AlertDialogRoot
    :open="open"
    @update:open="handleOpenChange"
  >
    <AlertDialogPortal>
      <AlertDialogOverlay class="modal-scrim" />
      <AlertDialogContent class="confirm-panel">
        <div class="confirm-panel__icon">
          <AlertTriangle
            :size="22"
            aria-hidden="true"
          />
        </div>
        <div class="confirm-panel__body">
          <AlertDialogTitle class="confirm-panel__title">
            Revoke certificate
          </AlertDialogTitle>
          <AlertDialogDescription class="confirm-panel__description">
            This will revoke "{{ certificateName }}". Existing bindings that depend on this certificate may stop validating.
          </AlertDialogDescription>
        </div>
        <div class="confirm-panel__actions">
          <AlertDialogCancel as-child>
            <button
              class="secondary-button"
              type="button"
              :disabled="busy"
            >
              Cancel
            </button>
          </AlertDialogCancel>
          <button
            class="danger-button"
            type="button"
            :disabled="busy"
            @click="emit('confirm')"
          >
            Revoke
          </button>
        </div>
      </AlertDialogContent>
    </AlertDialogPortal>
  </AlertDialogRoot>
</template>
