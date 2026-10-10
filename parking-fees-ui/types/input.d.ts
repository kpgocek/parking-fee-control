declare module 'cs2/input' {
  import React from 'react';

  export type InputAction = string;

  export interface SingleActionConsumerProps {
    action?: InputAction;
    actionContext?: string;
    disabled?: boolean;
    onAction?: () => void;
  }

  export interface InputActionConsumerAction {
    actionContext?: string;
    onAction?: () => void;
  }

  export type InputActionConsumerActions = Record<InputAction, (() => void) | InputActionConsumerAction | null | undefined>;

  export interface InputActionConsumerProps {
    actions?: InputActionConsumerActions;
    actionContext?: string;
    disabled?: boolean;
    ignoreFocusState?: boolean;
  }

  export const InputActionConsumer: React.FC<React.PropsWithChildren<InputActionConsumerProps>>;

  /** When the Keyboard "ESC" or Gamepad "B" button is pressed */
  export const BackConsumer: React.FC<React.PropsWithChildren<SingleActionConsumerProps>>;
  export const CloseConsumer: React.FC<React.PropsWithChildren<SingleActionConsumerProps>>;
  export const SelectConsumer: React.FC<React.PropsWithChildren<SingleActionConsumerProps>>;
  export const ExpandConsumer: React.FC<React.PropsWithChildren<SingleActionConsumerProps>>;

  export interface InputActionBarrierProps {
    includes?: InputAction[];
    excludes?: InputAction[];
    disabled?: boolean;
  }

  export const InputActionBarrier: React.FC<React.PropsWithChildren<InputActionBarrierProps>>;
}
