<script setup lang="ts">
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-vue-next';

export interface ToastMessage {
  id: number;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
}

defineProps<{
  messages: ToastMessage[];
}>();

const emit = defineEmits<{
  dismiss: [id: number];
}>();

const icons = {
  success: CheckCircle2,
  error: XCircle,
  warning: AlertTriangle,
  info: Info,
};
</script>

<template>
  <Teleport to="body">
    <div
      class="toast-stack"
      aria-live="polite"
    >
      <div
        v-for="message in messages"
        :key="message.id"
        class="toast"
        :class="`toast--${message.type}`"
      >
        <component
          :is="icons[message.type]"
          class="toast__icon"
          :size="18"
          aria-hidden="true"
        />
        <div class="toast__body">
          <div class="toast__title">
            {{ message.title }}
          </div>
          <div class="toast__message">
            {{ message.message }}
          </div>
        </div>
        <button
          class="toast__dismiss"
          type="button"
          title="Dismiss notification"
          @click="emit('dismiss', message.id)"
        >
          <X
            :size="16"
            aria-hidden="true"
          />
        </button>
      </div>
    </div>
  </Teleport>
</template>
