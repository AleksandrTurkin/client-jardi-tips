export function scrollCategories(scroller, direction) {
    const firstItem = scroller.querySelector('.category-scroller__item');
    const styles = getComputedStyle(scroller);
    const gap = Number.parseFloat(styles.columnGap) || 0;
    const distance = firstItem ? firstItem.getBoundingClientRect().width + gap : scroller.clientWidth;

    scroller.scrollBy({ left: direction * distance, behavior: 'smooth' });
}
