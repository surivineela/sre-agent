import { IShimmerElement, Shimmer, ShimmerElementType } from '@fluentui/react/lib/Shimmer';
import { memo } from 'react';

const shimmerLine: IShimmerElement[] = [
  {
    type: ShimmerElementType.line,
    height: 30,
  },
];

const numberOfPromptStarters = 4;

const ChatLoading = () => {
  return (
    <>
      {Array.from({ length: numberOfPromptStarters }).map((_, index) => (
        <ShimmerComponent key={index} flexEnd={index % 2 === 0} index={index} />
      ))}
    </>
  );
};

const ShimmerComponent = ({ flexEnd, index }: { flexEnd: boolean; index: number }) => {
  return (
    <Shimmer
      key={`shimmer-prompt-${index}`}
      data-testid="shimmer-prompt"
      shimmerElements={shimmerLine}
      style={{ width: '60%', alignSelf: flexEnd ? 'flex-end' : 'flex-start' }}
    />
  );
};

export default memo(ChatLoading);
