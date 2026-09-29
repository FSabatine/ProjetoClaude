import type { UseFormReturnType } from '@mantine/form';
import type { UseMutationResult } from '@tanstack/react-query';
import { notifyError, notifySuccess } from './notify';

/**
 * Submits a Mantine form through a mutation with the standard feedback:
 * success notification, server field errors shown inline (400/409), friendly message otherwise.
 */
export function submitWithFeedback<TValues, TBody, TResult>({
  form,
  mutation,
  toBody,
  successMessage,
  errorTitle,
  onSuccess,
}: {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- generic over any form shape
  form: UseFormReturnType<TValues, any>;
  mutation: UseMutationResult<TResult, unknown, TBody>;
  toBody: (values: TValues) => TBody;
  successMessage: string;
  errorTitle: string;
  onSuccess: (result: TResult) => void;
}) {
  return form.onSubmit(
    (values) =>
      mutation.mutate(toBody(values), {
        onSuccess: (result) => {
          form.resetDirty();
          notifySuccess(successMessage);
          onSuccess(result);
        },
        onError: (error) => {
          const apiError = notifyError(error, errorTitle);
          if (Object.keys(apiError.fieldErrors).length > 0) form.setErrors(apiError.fieldErrors);
        },
      }),
    // Client-side validation failed: move focus to the first invalid field.
    (errors) => {
      const first = Object.keys(errors)[0];
      if (first) form.getInputNode(first)?.focus();
    },
  );
}
